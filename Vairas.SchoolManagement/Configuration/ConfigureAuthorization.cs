using Microsoft.AspNetCore.Authorization;
using Vairas.SchoolManagement.Security;

namespace Vairas.SchoolManagement.Configuration;

public static class ConfigureAuthorization
{
    private static readonly string[] WorkloadScopes = ["school.profile-provisioning", "school.eligibility.read", "school.participants.read", "school.vehicle-authority.read", "school.scheduling.read", "school.coordination.read", "school.learning.read", "school.notifications.read", "school.documents.read", "school.assistant.read"];

    public static IServiceCollection AddEntryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SchoolEntry", policy => policy.AddRequirements(new EntryRequirement()));
            foreach (var scope in WorkloadScopes)
                options.AddPolicy(scope, policy => policy.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();
        return services;
    }
}
