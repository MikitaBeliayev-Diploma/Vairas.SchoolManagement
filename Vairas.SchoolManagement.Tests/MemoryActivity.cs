using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

public sealed class MemoryActivity : IUserActivity
{
    public List<Guid> Calls { get; } = []; public HashSet<Guid> Inactive { get; } = [];
    public Dictionary<Guid, ProfileBinding> Bindings { get; } = [];
    public Task RequireActiveAsync(Guid user, CancellationToken ct)
    {
        Calls.Add(user);
        if (Inactive.Contains(user))
            throw SchoolErrors.Denied();
        return Task.CompletedTask;
    }
    public Task<ProfileBinding> BindingAsync(string kind, Guid profile, CancellationToken ct) => Task.FromResult(Bindings.TryGetValue(profile, out var b) ? b : throw SchoolErrors.Dependency());
    public UserRoleProfiles? Profiles { get; set; }
    public Task<UserRoleProfiles> RoleProfilesAsync(Guid user, CancellationToken ct) => Task.FromResult(Profiles ?? throw SchoolErrors.Dependency());
}
