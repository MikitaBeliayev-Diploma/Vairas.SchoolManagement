using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class ProvisioningIssuerTests : BusinessFixture
{
    [Test]
    public async Task LocalValidationReturnsImmutableIssuerWithoutCallingUserManagement()
    {
        var issued = await Issue();
        var request = new SchoolAuthorityRequest(Target, Target, School, issued.Resource.InvitationId, SchoolRole.Teacher, false);
        var result = await Service.ProvisioningAsync(request, default);
        Assert.That(result.IssuerUserId, Is.EqualTo(AdminUser));
        Assert.That(result.Allowed, Is.True);
        Assert.That(Activity.Calls, Is.Empty);

        Store.Rows.OfType<SchoolMembership>().Single().UserId = Other;
        result = await Service.ProvisioningAsync(request, default);
        Assert.That(result.Allowed, Is.False);
        Assert.That(result.IssuerUserId, Is.Null);
    }

    [Test]
    public async Task OperatorAndBootstrapBasesReturnTheirActualIssuer()
    {
        Access.PlatformOperatorUserIds = [AdminUser];
        var issued = await Issue();
        var admission = await Service.ProvisioningAsync(new(Target, Target, School, issued.Resource.InvitationId, SchoolRole.Teacher, false), default);
        Assert.That(admission.IssuerUserId, Is.EqualTo(AdminUser));
        Store.Rows.OfType<SchoolMembership>().Single().EndedAt = Clock.Now;
        var bootstrap = await Service.ProvisioningAsync(new(AdminUser, Target, School, null, SchoolRole.Administrator, true), default);
        Assert.That(bootstrap.Allowed, Is.True);
        Assert.That(bootstrap.IssuerUserId, Is.EqualTo(AdminUser));
        Assert.That(Activity.Calls, Is.Empty);
    }
}
