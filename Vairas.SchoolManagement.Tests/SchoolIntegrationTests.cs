using System.Net;
using Vairas.Authentication.Business;
using Vairas.UserManagement.Persistance.DataAccess;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Npgsql;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Persistance;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Integration"), NonParallelizable]
public sealed class SchoolIntegrationTests
{
    [Test]
    public async Task Administrator_can_discover_school_invitations_with_keyset_pagination()
    {
        var school = await env.SeedSchool();
        var route = $"/api/v1/schools/{school}/invitations";
        using var issued = await env.Request(HttpMethod.Post, route, env.Administrator, new { targetUserId = env.Target.UserId, role = "Teacher" }, Guid.NewGuid());
        Assert.That(issued.IsSuccessStatusCode, Is.True, await issued.Content.ReadAsStringAsync());
        using var page = await env.Request(HttpMethod.Get, route + "?limit=1", env.Administrator);
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK), await page.Content.ReadAsStringAsync());
        var json = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(json.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
        Assert.That(json.GetProperty("items")[0].TryGetProperty("token", out _), Is.False);
        using var denied = await env.Request(HttpMethod.Get, route, env.Target);
        Assert.That(denied.StatusCode, Is.AnyOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound));
        using var invalid = await env.Request(HttpMethod.Get, route + "?cursor=invalid", env.Administrator);
        Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
    [Test]
    public async Task Inactive_invitation_issuer_cannot_authorize_first_staff_profile()
    {
        var issuer = await env.NewUser();
        var target = await env.NewUser();
        var school = await env.SeedSchool();
        var access = env.Factory.Services.GetRequiredService<SchoolAccessOptions>();
        var original = access.PlatformOperatorUserIds;
        try
        {
            access.PlatformOperatorUserIds = [.. original, issuer.UserId];
            using var issued = await env.Request(HttpMethod.Post, $"/api/v1/schools/{school:D}/invitations", issuer,
                new { targetUserId = target.UserId, role = "Teacher" }, Guid.NewGuid());
            Assert.That(issued.StatusCode, Is.EqualTo(HttpStatusCode.Created), await issued.Content.ReadAsStringAsync());
            var invitation = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("invitationId").GetGuid();
            await IntegrationEnvironment.Sql(env.UserOwner,
                $"UPDATE user_profile SET status='Inactive',revision=revision+1,updated_at=clock_timestamp() WHERE user_id='{issuer.UserId:D}'");
            using var result = await IntegrationEnvironment.Send(env.Users, HttpMethod.Post, "/api/v1/users/me/teacher-profile",
                target.Tokens.AccessToken, new { schoolId = school, invitationId = invitation }, Guid.NewGuid());
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), await result.Content.ReadAsStringAsync());
            await using var connection = new NpgsqlConnection(env.UserOwner);
            await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM teacher_profile WHERE user_id = @user", connection);
            query.Parameters.AddWithValue("user", target.UserId);
            Assert.That(await query.ExecuteScalarAsync(), Is.EqualTo(0L));
            await using var db = env.OwnerDb();
            Assert.That(await db.Set<SchoolMembership>().AnyAsync(x => x.SchoolId == school && x.UserId == target.UserId), Is.False);
        }
        finally { access.PlatformOperatorUserIds = original; }
    }
    private IntegrationEnvironment env = null!;
    [OneTimeSetUp]
    public async Task Start()
    {
        env = new();
        await env.StartAsync();
        TestContext.Out.WriteLine(env.Version);
    }
    [Test]
    public async Task Deactivation_reviews_use_real_discovery_and_are_tenant_scoped_and_append_only()
    {
        var school = await env.SeedSchool(); var user = Guid.NewGuid(); var membership = Guid.NewGuid();
        await using (var db = env.OwnerDb())
        {
            var operation = await db.Set<SchoolOperation>().Where(x => x.SchoolId == school).Select(x => x.OperationId).FirstAsync();
            db.Add(new SchoolMembership { SchoolId = school, MembershipId = membership, UserId = user, Role = SchoolRole.Teacher, Revision = 1, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), LastOperationId = operation });
            await db.SaveChangesAsync();
        }
        var store = new SchoolStore(new Microsoft.EntityFrameworkCore.Infrastructure.PooledDbContextFactory<SchoolDbContext>(new DbContextOptionsBuilder<SchoolDbContext>().UseSchool(env.SchoolRuntime).Options));
        var service = new SchoolService(store, new MemoryActivity { Profiles = new(user, "Inactive", 2, null, Guid.NewGuid(), null) }, new SchoolAccessOptions(), TimeProvider.System);
        var message = new UserDeactivatedEvent(Guid.NewGuid(), user, 2, DateTimeOffset.UtcNow, new string('d', 64));
        Assert.That(await service.ConsumeUserDeactivatedAsync(message, default), Is.EqualTo(new DeactivationOutcome("ReviewsRaised", 1)));
        Assert.That(await service.ConsumeUserDeactivatedAsync(message, default), Is.EqualTo(new DeactivationOutcome("ReviewsRaised", 1)));
        await using (var db = env.OwnerDb())
        {
            var review = await db.Set<MembershipReview>().SingleAsync(x => x.TargetId == membership);
            Assert.That((review.SchoolId, review.SubjectRole), Is.EqualTo((school, "Teacher")));
            Assert.That(await db.Set<SchoolMembership>().Where(x => x.MembershipId == membership).Select(x => x.EndedAt).SingleAsync(), Is.Null);
        }
        await using (var other = await store.OpenAsync(Guid.NewGuid(), false, default)) Assert.That(await other.ListAsync<MembershipReview>(x => true, default), Is.Empty);
        var scope = $"SELECT set_config('vairas.school_id','{school}',false);";
        await IntegrationEnvironment.Sql(env.SchoolRuntime, scope + $"UPDATE membership_review SET resolved_at=clock_timestamp(),resolved_by_user_id='{Guid.NewGuid()}',resolution_note='Ended' WHERE target_id='{membership}'");
        Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolRuntime, scope + $"UPDATE membership_review SET resolution_note='Rewritten' WHERE target_id='{membership}'"));
        Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolRuntime, scope + $"DELETE FROM membership_review WHERE target_id='{membership}'"));
        Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolOwner, $"UPDATE integration_inbox SET outcome='Forged' WHERE event_id='{message.EventId}'"));
    }
    [OneTimeTearDown]
    public async Task Stop()
    {
        if (env is not null)
            await env.DisposeAsync();
    }
    [Test]
    public async Task Readiness_and_actual_openapi_are_available()
    {
        using var authHealth = await env.Auth.GetAsync("/health/ready");
        Assert.That(authHealth.StatusCode, Is.EqualTo(HttpStatusCode.OK), await authHealth.Content.ReadAsStringAsync());
        using var userHealth = await env.Users.GetAsync("/health/ready");
        Assert.That(userHealth.StatusCode, Is.EqualTo(HttpStatusCode.OK), await userHealth.Content.ReadAsStringAsync());
        using var health = await env.Client.GetAsync("/health/ready");
        Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK), await health.Content.ReadAsStringAsync());
        using var live = await env.Client.GetAsync("/health/live");
        Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var response = await env.Client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), json);
        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("paths").EnumerateObject().Count(), Is.GreaterThan(15));
        var parsed = OpenApiDocument.Parse(json, "json");
        Assert.That(parsed.Diagnostic!.Errors, Is.Empty);
        Assert.That(parsed.Document!.Validate(ValidationRuleSet.GetDefaultRuleSet()).ToArray(), Is.Empty);
        Directory.CreateDirectory(Path.Combine(IntegrationEnvironment.Root, ".cache"));
        await File.WriteAllTextAsync(Path.Combine(IntegrationEnvironment.Root, ".cache/school-management.openapi.json"), json);
    }
    [Test]
    public async Task Vehicle_authority_is_workload_scoped_and_uses_real_Auth_UM_and_School_state()
    {
        var school = await env.SeedSchool();
        var token = await env.Workload("vehicle-management", "school.vehicle-authority.read");
        using var allowed = await IntegrationEnvironment.Send(env.Client, HttpMethod.Post, "/internal/v1/vehicle-authority/validate", token,
            new { schoolId = school, actorUserId = env.Administrator.UserId });
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await allowed.Content.ReadAsStringAsync());
        var basis = await allowed.Content.ReadFromJsonAsync<VehicleAuthorityResponse>();
        Assert.That(basis!.Allowed, Is.True); Assert.That(basis.ActorUserId, Is.EqualTo(env.Administrator.UserId));
        using var denied = await IntegrationEnvironment.Send(env.Client, HttpMethod.Post, "/internal/v1/vehicle-authority/validate", token,
            new { schoolId = school, actorUserId = env.Outsider.UserId });
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await denied.Content.ReadFromJsonAsync<VehicleAuthorityResponse>())!.Allowed, Is.False);
        using var user = await IntegrationEnvironment.Send(env.Client, HttpMethod.Post, "/internal/v1/vehicle-authority/validate", env.Administrator.Tokens.AccessToken,
            new { schoolId = school, actorUserId = env.Administrator.UserId });
        Assert.That(user.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    [Test]
    public async Task Real_Auth_and_UM_admit_actor_but_profile_without_membership_does_not()
    {
        var school = await env.SeedSchool();
        using var allowed = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", env.Administrator);
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await allowed.Content.ReadAsStringAsync());
        using var denied = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", env.Outsider);
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), await denied.Content.ReadAsStringAsync());
        using var anonymous = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", null);
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Foreign_school_lifecycle_is_not_disclosed(bool inactive)
    {
        var school = await env.SeedSchool();
        if (inactive)
            await IntegrationEnvironment.Sql(env.SchoolOwner, $"UPDATE school SET status='Inactive',revision=revision+1,updated_at=clock_timestamp() WHERE school_id='{school:D}'");
        using var foreign = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school:D}", env.Outsider);
        using var absent = await env.Request(HttpMethod.Get, $"/api/v1/schools/{Guid.NewGuid():D}", env.Outsider);
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(absent.StatusCode, Is.EqualTo(foreign.StatusCode));
        var foreignBody = await foreign.Content.ReadFromJsonAsync<JsonElement>();
        var absentBody = await absent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(foreignBody.GetProperty("code").GetString(), Is.EqualTo(absentBody.GetProperty("code").GetString()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Self_invitation_filter_does_not_disclose_foreign_school_existence(bool inactive)
    {
        var school = await env.SeedSchool();
        if (inactive)
            await IntegrationEnvironment.Sql(env.SchoolOwner, $"UPDATE school SET status='Inactive',revision=revision+1,updated_at=clock_timestamp() WHERE school_id='{school:D}'");
        using var foreign = await env.Request(HttpMethod.Get, $"/api/v1/users/me/invitations?schoolId={school:D}", env.Outsider);
        using var absent = await env.Request(HttpMethod.Get, $"/api/v1/users/me/invitations?schoolId={Guid.NewGuid():D}", env.Outsider);
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(absent.StatusCode, Is.EqualTo(foreign.StatusCode));
        foreach (var response in new[] { foreign, absent })
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(body.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(body.GetProperty("nextCursor").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
    }

    [Test]
    public async Task Invitations_use_real_UM_School_provider_and_gate_denies_new_membership()
    {
        var school = await env.SeedSchool();
        var collection = $"/api/v1/schools/{school}/invitations";
        using var issued = await env.Request(HttpMethod.Post, collection, env.Administrator, new
        {
            targetUserId = env.Target.UserId,
            role = "Teacher"
        }, Guid.NewGuid());
        Assert.That(issued.StatusCode, Is.EqualTo(HttpStatusCode.Created), await issued.Content.ReadAsStringAsync());
        var invitation = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("invitationId").GetGuid();
        using var profile = await IntegrationEnvironment.Send(env.Users, HttpMethod.Post, "/api/v1/users/me/teacher-profile", env.Target.Tokens.AccessToken, new
        {
            schoolId = school,
            invitationId = invitation
        }, Guid.NewGuid());
        Assert.That(profile.StatusCode, Is.EqualTo(HttpStatusCode.Created), await profile.Content.ReadAsStringAsync());
        using var accept = await env.Request(HttpMethod.Post, $"{collection}/{invitation}/accept", env.Target, new
        {
            expectedRevision = 1
        }, Guid.NewGuid());
        Assert.That(accept.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable), await accept.Content.ReadAsStringAsync());
        Assert.That((await accept.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString(), Is.EqualTo("ACTIVATION_INTEGRATION_NOT_READY"));
        await using var db = env.OwnerDb();
        Assert.That((await db.Set<SchoolInvitation>().SingleAsync(x => x.InvitationId == invitation)).Status, Is.EqualTo(InvitationStatus.Pending));
        Assert.That(await db.Set<SchoolMembership>().AnyAsync(x => x.SchoolId == school && x.UserId == env.Target.UserId), Is.False);
    }
    [Test]
    public async Task Concurrent_identical_creates_commit_once_and_changed_intent_conflicts()
    {
        var school = await env.SeedSchool();
        var operation = Guid.NewGuid();
        var route = $"/api/v1/schools/{school}/branches";
        var tasks = Enumerable.Range(0, 6).Select(_ => env.Request(HttpMethod.Post, route, env.Administrator, new { name = "Concurrent" }, operation)).ToArray();
        var responses = await Task.WhenAll(tasks);
        try
        {
            foreach (var r in responses)
                Assert.That(r.StatusCode, Is.AnyOf(HttpStatusCode.Created, HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
            Assert.That(responses.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
        }
        finally { foreach (var r in responses) r.Dispose(); }
        await using var db = env.OwnerDb();
        Assert.That(await db.Set<Branch>().CountAsync(x => x.SchoolId == school), Is.EqualTo(1));
        using var changed = await env.Request(HttpMethod.Post, route, env.Administrator, new
        {
            name = "Different"
        }, operation);
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await changed.Content.ReadAsStringAsync());
    }
    [Test]
    public async Task Runtime_RLS_has_no_scope_leak_and_cannot_delete_or_create_objects()
    {
        var school = await env.SeedSchool();
        var other = await env.SeedSchool();
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT count(*) FROM school"), Is.EqualTo("0"));
        await using (var connection = new NpgsqlConnection(env.SchoolRuntime))
        {
            await connection.OpenAsync();
            await using var tx = await connection.BeginTransactionAsync();
            await using var scope = new NpgsqlCommand("SELECT set_config('vairas.school_id',@school,true)", connection, tx);
            scope.Parameters.AddWithValue("school", school.ToString("D"));
            await scope.ExecuteNonQueryAsync();
            await using var count = new NpgsqlCommand("SELECT count(*) FROM school", connection, tx);
            Assert.That((long)(await count.ExecuteScalarAsync())!, Is.EqualTo(1));
            await tx.RollbackAsync();
        }
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT count(*) FROM school"), Is.EqualTo("0"));
        var error = Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolRuntime, "DELETE FROM school"))!;
        Assert.That(error.SqlState, Is.EqualTo("42501"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT has_schema_privilege('public','CREATE')"), Is.EqualTo("False"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT has_database_privilege(current_database(),'TEMP')"), Is.EqualTo("False"));
        var create = Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolRuntime, "CREATE TABLE public.forbidden_runtime_table(id uuid)"))!;
        Assert.That(create.SqlState, Is.EqualTo("42501"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND rowsecurity AND tablename NOT IN ('school_coordination','school_learning_outbox','membership_review')"), Is.EqualTo("13"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT string_agg(tablename||':'||rowsecurity,',' ORDER BY tablename) FROM pg_tables WHERE schemaname='public' AND tablename IN ('membership_review','integration_inbox')"), Is.EqualTo("integration_inbox:false,membership_review:true"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime, "SELECT rowsecurity FROM pg_tables WHERE schemaname='public' AND tablename='school_learning_outbox'"), Is.EqualTo("True"));
    }
    [Test]
    public async Task Native_overlap_and_immutable_evidence_are_enforced()
    {
        var school = await env.SeedSchool();
        await using var db = env.OwnerDb();
        var membership = await db.Set<SchoolMembership>().SingleAsync(x => x.SchoolId == school);
        var operation = membership.LastOperationId;
        await using (var duplicate = env.OwnerDb())
        {
            duplicate.Add(new SchoolMembership { SchoolId = school, MembershipId = Guid.NewGuid(), UserId = membership.UserId, Role = membership.Role, StartedAt = membership.StartedAt.AddSeconds(1), Revision = 1, LastOperationId = operation });
            var error = Assert.ThrowsAsync<DbUpdateException>(async () => await duplicate.SaveChangesAsync())!;
            Assert.That((error.InnerException as PostgresException)?.SqlState, Is.EqualTo("23P01"));
        }
        using var created = await env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/groups", env.Administrator, new
        {
            name = "Empty"
        }, Guid.NewGuid());
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        var group = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("groupId").GetGuid();
        var immutable = Assert.ThrowsAsync<PostgresException>(async () => await IntegrationEnvironment.Sql(env.SchoolOwner, $"UPDATE group_roster_revision SET recorded_at=clock_timestamp() WHERE group_id='{group}'"))!;
        Assert.That(immutable.SqlState, Is.EqualTo("23514"));
    }
    [Test]
    public async Task Malformed_missing_and_untrusted_input_returns_problem_details()
    {
        var school = await env.SeedSchool();
        var route = $"/api/v1/schools/{school}/invitations";
        foreach (var body in new object[] { new { targetUserId = env.Target.UserId }, new { targetUserId = env.Target.UserId, role = "Teacher", status = "Accepted" }, new { targetUserId = Guid.Empty, role = "Teacher" } })
        {
            using var r = await env.Request(HttpMethod.Post, route, env.Administrator, body, Guid.NewGuid());
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await r.Content.ReadAsStringAsync());
            Assert.That(r.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
            var p = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(p.TryGetProperty("correlationId", out _), Is.True);
        }
        using var noKey = await env.Request(HttpMethod.Post, route, env.Administrator, new
        {
            targetUserId = env.Target.UserId,
            role = "Teacher"
        });
        Assert.That(noKey.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
    [Test]
    public async Task Missing_dependency_denies_new_admission()
    {
        var school = await env.SeedSchool();
        try
        {
            env.FailUsers = true;
            using var r = await env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/branches", env.Administrator, new
            {
                name = "Must not exist"
            }, Guid.NewGuid());
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable), await r.Content.ReadAsStringAsync());
        }
        finally { env.FailUsers = false; }
        await using var db = env.OwnerDb();
        Assert.That(await db.Set<Branch>().AnyAsync(x => x.SchoolId == school), Is.False);
    }
    [Test]
    public async Task Workload_scope_and_client_allowlist_are_independent_of_user_token()
    {
        var school = await env.SeedSchool();
        var token = await env.Workload("user-management", "identity.validate");
        using var denied = await IntegrationEnvironment.Send(env.Client, HttpMethod.Post, "/internal/v1/profile-provisioning/validate", token, new
        {
            actorUserId = env.Target.UserId,
            targetUserId = env.Target.UserId,
            schoolId = school,
            invitationId = (Guid?)null,
            role = "Teacher",
            bootstrap = false
        });
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized).Or.EqualTo(HttpStatusCode.Forbidden));
        token = await env.Workload("user-management", "school.profile-provisioning");
        using var allowed = await IntegrationEnvironment.Send(env.Client, HttpMethod.Post, "/internal/v1/profile-provisioning/validate", token, new
        {
            actorUserId = env.Target.UserId,
            targetUserId = env.Target.UserId,
            schoolId = school,
            invitationId = (Guid?)null,
            role = "Teacher",
            bootstrap = false
        });
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await allowed.Content.ReadAsStringAsync());
        Assert.That((await allowed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("allowed").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Staff_reuse_accept_revoke_race_has_one_terminal_result()
    {
        var school = await env.SeedSchool(true);
        using var issue = await env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/invitations", env.Administrator, new
        {
            targetUserId = env.Target.UserId,
            role = "Teacher"
        }, Guid.NewGuid());
        Assert.That(issue.StatusCode, Is.EqualTo(HttpStatusCode.Created), await issue.Content.ReadAsStringAsync());
        var id = (await issue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("invitationId").GetGuid();
        using var profile = await IntegrationEnvironment.Send(env.Users, HttpMethod.Post, "/api/v1/users/me/teacher-profile", env.Target.Tokens.AccessToken, new
        {
            schoolId = school,
            invitationId = id
        }, Guid.NewGuid());
        Assert.That(profile.IsSuccessStatusCode, Is.True, await profile.Content.ReadAsStringAsync());
        var accept = env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/invitations/{id}/accept", env.Target, new
        {
            expectedRevision = 1
        }, Guid.NewGuid());
        var revoke = env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/invitations/{id}/revoke", env.Administrator, new
        {
            expectedRevision = 1,
            reason = "race"
        }, Guid.NewGuid());
        var responses = await Task.WhenAll(accept, revoke);
        try
        {
            Assert.That(responses.Count(x => x.IsSuccessStatusCode), Is.EqualTo(1), string.Join("\n", await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))));
        }
        finally { foreach (var r in responses) r.Dispose(); }
        await using var db = env.OwnerDb();
        var i = await db.Set<SchoolInvitation>().SingleAsync(x => x.InvitationId == id);
        Assert.That(i.Status, Is.AnyOf(InvitationStatus.Accepted, InvitationStatus.Revoked));
        Assert.That(await db.Set<SchoolMembership>().CountAsync(x => x.SchoolId == school && x.UserId == env.Target.UserId), Is.EqualTo(1));
    }
    [Test]
    public async Task Global_deactivation_denies_next_admission_without_withdrawing_school_membership()
    {
        var user = await env.NewUser();
        var school = await env.SeedSchool();
        using var scope = env.Factory.Services.CreateScope();
        env.Factory.Services.GetRequiredService<SchoolAccessOptions>().PlatformOperatorUserIds = [env.Administrator.UserId, user.UserId];
        using (var allowed = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", user))
            Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await allowed.Content.ReadAsStringAsync());
        // Fixture controls UM primary state; the School admission path remains real HTTP.
        await IntegrationEnvironment.Sql(env.UserOwner, $"UPDATE user_profile SET status='Inactive',revision=revision+1,updated_at=GREATEST(created_at,clock_timestamp()) WHERE user_id='{user.UserId:D}'");
        using var denied = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", user);
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), await denied.Content.ReadAsStringAsync());
    }
    [Test]
    public async Task Revoked_session_cannot_use_still_unexpired_signed_user_JWT()
    {
        var user = await env.NewUser();
        var school = await env.SeedSchool();
        using (var scope = env.AuthFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAuthenticationService>().RevokeAsync(new(user.Tokens.IdentityId, user.Tokens.SessionId), user.Tokens.SessionId, Guid.NewGuid(), new(Guid.NewGuid()), default);
        using var result = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}", user);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), await result.Content.ReadAsStringAsync());
    }
    [Test]
    public async Task Workload_JWT_cannot_impersonate_interactive_user()
    {
        var school = await env.SeedSchool();
        var token = await env.Workload("user-management", "school.profile-provisioning");
        using var r = await IntegrationEnvironment.Send(env.Client, HttpMethod.Get, $"/api/v1/schools/{school}", token);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    [Test]
    public async Task Same_key_between_two_authorized_tenants_is_independent_and_foreign_branch_is_rejected()
    {
        var a = await env.SeedSchool();
        var b = await env.SeedSchool();
        var key = Guid.NewGuid();
        Guid branch;
        using (var first = await env.Request(HttpMethod.Post, $"/api/v1/schools/{a}/branches", env.Administrator, new
        {
            name = "A"
        }, key))
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created), await first.Content.ReadAsStringAsync());
            branch = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("branchId").GetGuid();
        }
        using (var second = await env.Request(HttpMethod.Post, $"/api/v1/schools/{b}/branches", env.Administrator, new
        {
            name = "B"
        }, key))
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created), await second.Content.ReadAsStringAsync());
        using var foreign = await env.Request(HttpMethod.Post, $"/api/v1/schools/{b}/groups", env.Administrator, new
        {
            name = "Invalid",
            branchId = branch
        }, Guid.NewGuid());
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), await foreign.Content.ReadAsStringAsync());
    }
    [Test]
    public async Task Self_discovery_returns_only_exact_target_and_cannot_be_influenced_by_query_userId()
    {
        var school = await env.SeedSchool();
        Guid id;
        using (var issued = await env.Request(HttpMethod.Post, $"/api/v1/schools/{school}/invitations", env.Administrator, new
        {
            targetUserId = env.Target.UserId,
            role = "Student",
            categoryCode = "B"
        }, Guid.NewGuid()))
        {
            Assert.That(issued.StatusCode, Is.EqualTo(HttpStatusCode.Created), await issued.Content.ReadAsStringAsync());
            id = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("invitationId").GetGuid();
        }
        using (var own = await env.Request(HttpMethod.Get, $"/api/v1/users/me/invitations?schoolId={school}", env.Target))
        {
            Assert.That(own.StatusCode, Is.EqualTo(HttpStatusCode.OK), await own.Content.ReadAsStringAsync());
            Assert.That((await own.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength(), Is.EqualTo(1));
        }
        using var foreign = await env.Request(HttpMethod.Get, $"/api/v1/users/me/invitations?schoolId={school}&userId={env.Target.UserId}", env.Outsider);
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength(), Is.Zero);
        using var read = await env.Request(HttpMethod.Get, $"/api/v1/schools/{school}/invitations/{id}", env.Outsider);
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
    [Test]
    public async Task Invitation_keyset_limits_each_school_and_matches_Guid_order_on_PostgreSql()
    {
        var schools = new[] { await env.SeedSchool(), await env.SeedSchool() };
        var target = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var ids = new[] { "ffffffff-ffff-ffff-ffff-ffffffffffff", "80000000-8000-8000-8000-800000000000", "7fffffff-ffff-ffff-ffff-ffffffffffff", "00000001-0000-0000-0000-000000000000" }.Select(Guid.Parse).ToArray();
        var expected = new List<(Guid School, Guid Id)>();
        await using var db = env.OwnerDb();
        foreach (var school in schools)
            foreach (var id in ids)
            {
                var op = Guid.NewGuid();
                db.Add(new SchoolOperation { SchoolId = school, OperationId = op, CommandName = "IsolatedPaginationFixture", TargetKind = "SchoolInvitation", TargetId = id, ActorUserId = env.Administrator.UserId, Channel = OperationChannel.Internal, Status = OperationStatus.Committed, RequestFingerprint = new string('a', 64), RequestedAt = now, ResolvedAt = now, AuthorityBasis = "synthetic test fixture", OutcomeCode = "TEST_ONLY" });
                db.Add(new SchoolInvitation { SchoolId = school, InvitationId = id, TargetUserId = target, Role = SchoolRole.Teacher, Revision = 1, Status = InvitationStatus.Pending, CreatedAt = now, ExpiresAt = now.AddDays(1), IssuedOperationId = op });
                expected.Add((school, id));
            }
        await db.SaveChangesAsync();
        var seen = new List<(Guid School, Guid Id)>();
        InvitationCursor? cursor = null;
        while (true)
        {
            var candidates = new List<SchoolInvitation>();
            foreach (var school in schools)
            {
                var rows = await SchoolStore.OwnInvitationPage(db, school, target, 3, cursor).ToArrayAsync();
                Assert.That(rows.Length, Is.LessThanOrEqualTo(3));
                candidates.AddRange(rows);
            }
            var page = candidates.OrderBy(x => x.CreatedAt).ThenBy(x => x.SchoolId).ThenBy(x => x.InvitationId).Take(2).ToArray();
            if (page.Length == 0) break;
            seen.AddRange(page.Select(x => (x.SchoolId, x.InvitationId)));
            Assert.That(seen.Count, Is.LessThanOrEqualTo(expected.Count), "Database cursor did not advance.");
            var last = page.Last();
            cursor = new(last.CreatedAt, last.SchoolId, last.InvitationId);
        }
        Assert.That(seen, Is.EqualTo(expected.OrderBy(x => x.School).ThenBy(x => x.Id)));
    }

    [Test]
    public async Task EF_snapshot_and_canonical_column_types_match_the_migrated_database()
    {
        await using var db = env.OwnerDb();
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        var columns = await IntegrationEnvironment.Scalar(env.SchoolOwner, "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name NOT IN ('__EFMigrationsHistory','school_coordination','school_learning_outbox','membership_review','integration_inbox')");
        Assert.That(columns, Is.EqualTo("137"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolOwner,"SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='school_learning_outbox'"),Is.EqualTo("13"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolOwner,"SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('membership_review','integration_inbox')"),Is.EqualTo("19"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolOwner,"SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='school_coordination'"),Is.EqualTo("14"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolRuntime,"SELECT rowsecurity FROM pg_tables WHERE schemaname='public' AND tablename='school_coordination'"),Is.EqualTo("True"));
        Assert.That(await IntegrationEnvironment.Scalar(env.SchoolOwner, "SELECT count(*) FROM pg_constraint WHERE contype='x'"), Is.EqualTo("4"));
    }

}

