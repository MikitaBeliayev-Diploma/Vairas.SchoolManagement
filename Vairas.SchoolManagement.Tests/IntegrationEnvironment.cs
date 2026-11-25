extern alias Authentication;
extern alias UserManagement;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using Vairas.Authentication.Business;
using Vairas.Authentication.Domain;
using Vairas.Authentication.Persistance;
using Vairas.UserManagement.Persistance.DataAccess;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Persistance;
using AuthProgram = Authentication::Program;
using UserProgram = UserManagement::Program;
using AuthHasher = Authentication::Vairas.Authentication.Security.PasswordHasher;
using AuthProtector = Vairas.Authentication.Business.ISecretProtector;
using UmDependencies = UserManagement::Vairas.UserManagement.Configuration.Options.DependencyOptions;
using UmJwt = UserManagement::Vairas.UserManagement.Configuration.Options.JwtOptions;
using UmOutbox = UserManagement::Vairas.UserManagement.Configuration.Options.OutboxOptions;
using Vairas.SchoolManagement.Configuration.Options;

namespace Vairas.SchoolManagement.Tests;
public sealed class IntegrationEnvironment : IAsyncDisposable
{
    private PostgreSqlContainer? container; private string admin = ""; private string owner = ""; private string runtime = ""; private readonly List<string> databases = [];
    public string SchoolOwner { get; private set; } = ""; public string SchoolRuntime { get; private set; } = ""; public string AuthOwner { get; private set; } = ""; public string UserOwner { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Secret { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Temp { get; } = Path.Combine(Path.GetTempPath(), "vairas-school-tests-" + Guid.NewGuid().ToString("N"));
    public HttpClient Auth { get; private set; } = null!; public HttpClient Users { get; private set; } = null!; public HttpClient Client { get; private set; } = null!;
    public ConfiguredFactory<AuthProgram> AuthFactory { get; private set; } = null!;
    private ConfiguredFactory<UserProgram> userFactory = null!;
    public ConfiguredFactory<Program> Factory { get; private set; } = null!;
    public TestUser Administrator { get; private set; } = null!; public TestUser Target { get; private set; } = null!; public TestUser Outsider { get; private set; } = null!;
    public Action<Dictionary<string,string?>>? ConfigureAuth {get;set;}
    public Action<Dictionary<string,string?>>? ConfigureSchool {get;set;}
    public Action<IServiceCollection>? ConfigureUsers {get;set;}
    public HttpClient? Scheduling {get;set;}
    public HttpClient? Learning {get;set;}
    public bool FailAuth
    {
        get; set;
    }
    public bool FailUsers
    {
        get; set;
    }
    public static string Root
    {
        get
        {
            var candidate=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
            return Directory.Exists(Path.Combine(candidate,"Vairas.SchoolManagement.Persistance"))?candidate:Path.GetFullPath(Path.Combine(candidate,"../Vairas.SchoolManagement"));
        }
    }
    public static string Backend => Path.GetDirectoryName(Root)!;
    public SchoolDbContext OwnerDb() => new(new DbContextOptionsBuilder<SchoolDbContext>().UseSchool(SchoolOwner).Options);
    public async Task StartAsync()
    {
        admin = Environment.GetEnvironmentVariable("VAIRAS_TEST_ADMIN_CONNECTION") ?? "";
        if (admin.Length == 0)
        {
            container = new PostgreSqlBuilder("postgres:17").WithPassword(Secret).Build();
            await container.StartAsync();
            admin = container.GetConnectionString();
        }
        Version = await Scalar(admin, "SELECT version()");
        var suffix = Guid.NewGuid().ToString("N");
        owner = "sm_owner_" + suffix;
        runtime = "sm_runtime_" + suffix;
        await Sql(admin, $"CREATE ROLE {owner} LOGIN PASSWORD '{Secret}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; CREATE ROLE {runtime} LOGIN PASSWORD '{Secret}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
        var school = await Database();
        SchoolOwner = school.Owner;
        SchoolRuntime = school.Runtime;
        await using (var db = OwnerDb())
        {
            await db.Database.MigrateAsync();
            // Frozen raw SQL creates native enums after this data source first inspected the empty database.
            await db.Database.OpenConnectionAsync();
            await ((NpgsqlConnection)db.Database.GetDbConnection()).ReloadTypesAsync();
        }
        foreach (var file in new[] { "database/runtime-grants.sql", "database/review-grants.sql" })
        {
            var grants = await File.ReadAllTextAsync(Path.Combine(Root, file));
            await Sql(SchoolOwner, grants.Replace("v_school_management_runtime", runtime).Replace("v_school_management_migration", owner));
        }
        var auth = await Database();
        AuthOwner = auth.Owner;
        await using (var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(auth.Owner).Options))
            await db.Database.MigrateAsync();
        await Grant(auth.Owner, "Vairas.Authentication", "runtime-grants.sql", "v_authentication");
        var users = await Database();
        UserOwner = users.Owner;
        var userOptions = new DbContextOptionsBuilder<VairasDbContext>();
        userOptions.UseVairasPostgres(users.Owner);
        await using (var db = new VairasDbContext(userOptions.Options))
            await db.Database.MigrateAsync();
        await Grant(users.Owner, "Vairas.UserManagement", "database-roles.sql", "v_user_management");
        Directory.CreateDirectory(Temp);
        var certPath = Path.Combine(Temp, "test.pfx");
        using (var rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest("CN=School Integration Tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
            await File.WriteAllBytesAsync(certPath, cert.Export(X509ContentType.Pfx, Secret));
        }
        using var hasher = new AuthHasher();
        var hash = hasher.Hash(Secret);
        var authConfig = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = auth.Runtime,
            ["Authentication:SigningCertificatePath"] = certPath,
            ["Authentication:SigningCertificatePassword"] = Secret,
            ["Authentication:UserAudiences:0"] = "vairas-authentication",
            ["Authentication:UserAudiences:1"] = "vairas-user-management",
            ["Authentication:UserAudiences:2"] = "vairas-school-management",
            ["Protection:KeyRingPath"] = Path.Combine(Temp, "keys"),
            ["Protection:CertificatePath"] = certPath,
            ["Protection:CertificatePassword"] = Secret,
            ["Smtp:Sender"] = "test@example.test",
            ["Smtp:ChallengePage"] = "https://frontend.test/challenge",
            ["Smtp:WorkerEnabled"] = "false",
            ["RateLimits:RequestsPerMinute"] = "10000"
        };
        var clients = new Dictionary<string, string[]>
        {
            ["school-management"] = ["identity.validate", "profiles.status.read", "profiles.binding.read"],
            ["user-management"] = ["identity.validate", "school.profile-provisioning", "confirmations.validate"],
            ["scheduling"] = ["school.eligibility.read", "school.participants.read"],
            ["vehicle-management"] = ["school.vehicle-authority.read"]
        };
        foreach (var (client, scopes) in clients)
        {
            authConfig[$"Authentication:WorkloadClients:{client}:SecretHash"] = hash;
            for (var i = 0; i < scopes.Length; i++)
                authConfig[$"Authentication:WorkloadClients:{client}:Scopes:{i}"] = scopes[i];
        }
        authConfig["Authentication:ScopeAudiences:school.eligibility.read:0"] = "vairas-school-management-internal";
        authConfig["Authentication:ScopeAudiences:school.participants.read:0"] = "vairas-school-management-internal";
        ConfigureAuth?.Invoke(authConfig);
        AuthFactory = new(Path.Combine(Backend, "Vairas.Authentication/Vairas.Authentication"), authConfig);
        Auth = AuthFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost:7100")
        });
        userFactory = new(Path.Combine(Backend, "Vairas.UserManagement/Vairas.UserManagement"), new(), services =>
        {
            services.RemoveAll<DbContextOptions<VairasDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<VairasDbContext>>();
            services.AddDbContext<VairasDbContext>(o => o.UseVairasPostgres(users.Runtime));
            services.Configure<UmDependencies>(o => { o.ClientSecret = Secret; });
            services.Configure<UmOutbox>(o => o.Enabled = false);
            services.Configure<UmJwt>(o => o.WorkloadClients["school-management"] = ["profiles.status.read", "profiles.binding.read"]);
            services.AddHttpClient("dependencies").ConfigurePrimaryHttpMessageHandler(() => new Bridge(this));
            ConfigureJwt(services);
            ConfigureUsers?.Invoke(services);
        });
        Users = userFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost:7001")
        });
        Administrator = await NewUser();
        Target = await NewUser();
        Outsider = await NewUser();
        var schoolConfig = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SchoolManagement"] = SchoolRuntime,
            ["Dependencies:ClientSecret"] = Secret,
            ["Access:PlatformOperatorUserIds:0"] = Administrator.UserId.ToString("D"),
            ["Authentication:WorkloadClients:user-management:0"] = "school.profile-provisioning",
            ["Authentication:WorkloadClients:scheduling:0"] = "school.eligibility.read",
            ["Authentication:WorkloadClients:scheduling:1"] = "school.participants.read",
            ["Authentication:WorkloadClients:vehicle-management:0"] = "school.vehicle-authority.read"
        };
        ConfigureSchool?.Invoke(schoolConfig);
        Factory = new(Path.Combine(Root, "Vairas.SchoolManagement"), schoolConfig, services =>
        {
            services.AddHttpClient("dependencies").ConfigurePrimaryHttpMessageHandler(() => new Bridge(this));
            ConfigureJwt(services);
        });
        Client = Factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost:7200")
        });
    }
    private void ConfigureJwt(IServiceCollection services)
    {
        foreach (var name in new[] { "User", "Workload" })
            services.PostConfigure<JwtBearerOptions>(name, o =>
        {
            o.Authority = "https://localhost:7100";
            o.MetadataAddress = "https://localhost:7100/.well-known/openid-configuration";
            o.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(o.MetadataAddress, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(Auth) { RequireHttps = true });
        });
    }
    public async Task<TestUser> NewUser()
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var password = "Synthetic-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using (var scope = AuthFactory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            await service.RegisterAsync(new(email, password), new(Guid.NewGuid()), default);
            await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(AuthOwner).Options);
            var delivery = await (from d in db.Set<ChallengeDelivery>() join c in db.Set<AccountChallenge>() on d.ChallengeId equals c.ChallengeId where c.LoginEmail == email select d).SingleAsync();
            var mail = JsonSerializer.Deserialize<ChallengeMail>(scope.ServiceProvider.GetRequiredService<AuthProtector>().Unprotect("mail", delivery.ProtectedPayload!))!;
            await service.CompleteAsync(new(mail.Token, null), new(Guid.NewGuid()), default);
        }
        using var login = await Auth.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK), await login.Content.ReadAsStringAsync());
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        using (var create = await Send(Users, HttpMethod.Post, "/api/v1/users/me", pair.AccessToken, new
        {
            firstName = "Synthetic",
            lastName = "User",
            emailAddress = (string?)null,
            phoneNumber = (string?)null
        }, Guid.NewGuid()))
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), await create.Content.ReadAsStringAsync());
        using var me = await Send(Users, HttpMethod.Get, "/api/v1/users/me", pair.AccessToken);
        var profile = await me.Content.ReadFromJsonAsync<JsonElement>();
        return new(profile.GetProperty("userId").GetGuid(), pair);
    }
    public async Task<Guid> SeedSchool(bool targetTeacher = false)
    {
        var school = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using var db = OwnerDb();
        db.Add(new School { SchoolId = school, Name = "Synthetic School", Revision = 1, Status = SchoolStatus.Active, CreatedAt = now, UpdatedAt = now });
        db.Add(new SchoolOperation { SchoolId = school, OperationId = operation, CommandName = "IsolatedTestFixture", TargetKind = "School", TargetId = school, ActorUserId = Administrator.UserId, Channel = OperationChannel.Internal, Status = OperationStatus.Committed, RequestFingerprint = new string('a', 64), RequestedAt = now, ResolvedAt = now, AuthorityBasis = "synthetic test fixture", OutcomeCode = "TEST_ONLY" });
        db.Add(new SchoolMembership { SchoolId = school, MembershipId = Guid.NewGuid(), UserId = Administrator.UserId, Role = SchoolRole.Administrator, Revision = 1, StartedAt = now, LastOperationId = operation });
        if (targetTeacher)
            db.Add(new SchoolMembership { SchoolId = school, MembershipId = Guid.NewGuid(), UserId = Target.UserId, Role = SchoolRole.Teacher, Revision = 1, StartedAt = now, LastOperationId = operation });
        await db.SaveChangesAsync();
        return school;
    }
    public Task<HttpResponseMessage> Request(HttpMethod method, string path, TestUser? user, object? body = null, Guid? op = null) => Send(Client, method, path, user?.Tokens.AccessToken, body, op);
    public static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token, object? body = null, Guid? op = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (op.HasValue)
            request.Headers.Add("Idempotency-Key", op.Value.ToString("D"));
        return await client.SendAsync(request);
    }
    public async Task<string> Workload(string client, string scope)
    {
        using var r = await Auth.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = client, ["client_secret"] = Secret, ["scope"] = scope }));
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
    }
    private async Task<(string Owner, string Runtime)> Database()
    {
        var name = "school_test_" + Guid.NewGuid().ToString("N");
        await Sql(admin, $"CREATE DATABASE {name} OWNER {owner}");
        databases.Add(name);
        await Sql(new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString,
            $"ALTER SCHEMA public OWNER TO {owner}");
        return (new NpgsqlConnectionStringBuilder(admin) { Database = name, Username = owner, Password = Secret }.ConnectionString, new NpgsqlConnectionStringBuilder(admin) { Database = name, Username = runtime, Password = Secret }.ConnectionString);
    }
    private async Task Grant(string connection, string service, string script, string rolePrefix)
    {
        var grants = await File.ReadAllTextAsync(Path.Combine(Backend, service, "database", script));
        await Sql(connection, grants.Replace(rolePrefix + "_runtime", runtime).Replace(rolePrefix + "_migration", owner));
    }
    public static async Task Sql(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, db);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<string> Scalar(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, db);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
    }
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        Users?.Dispose();
        if (userFactory is not null)
            await userFactory.DisposeAsync();
        Auth?.Dispose();
        if (AuthFactory is not null)
            await AuthFactory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        foreach (var db in databases)
            await Sql(admin, $"DROP DATABASE IF EXISTS {db} WITH (FORCE)");
        if (owner.Length > 0)
            await Sql(admin, $"DROP ROLE IF EXISTS {runtime}; DROP ROLE IF EXISTS {owner}");
        if (container is not null)
            await container.DisposeAsync();
    }
    private sealed class Bridge(IntegrationEnvironment env) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Host != "localhost")
                throw new HttpRequestException("Unexpected integration host.");
            var port = request.RequestUri.Port;
            if (port == 7100 && env.FailAuth || port == 7001 && env.FailUsers)
                return new(HttpStatusCode.ServiceUnavailable);
            var client = port switch
            {
                7100 => env.Auth,
                7001 => env.Users,
                7200 => env.Client,
                7300 => env.Scheduling ?? throw new HttpRequestException("Scheduling test pipeline is not attached."),
                7500 => env.Learning ?? throw new HttpRequestException("Learning test pipeline is not attached."),
                _ => throw new HttpRequestException("Unexpected integration port.")
            };
            using var forward = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var h in request.Headers)
                forward.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (request.Content is not null)
            {
                forward.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
                foreach (var h in request.Content.Headers)
                    forward.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            return await client.SendAsync(forward, ct);
        }
    }
}
