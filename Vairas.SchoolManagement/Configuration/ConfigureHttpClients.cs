using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;

namespace Vairas.SchoolManagement.Configuration;

public static class ConfigureHttpClients
{
    public static IServiceCollection AddDependencyHttpClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<DependencyCorrelationHandler>();
        services.AddHttpClient("dependencies", client => client.Timeout = TimeSpan.FromSeconds(5))
            .AddHttpMessageHandler<DependencyCorrelationHandler>();
        services.AddSingleton<IWorkloadTokens, WorkloadTokens>();
        services.AddScoped<IDependencyTransport, DependencyTransport>();
        services.AddScoped<IUserActivity, UserActivity>();
        services.AddScoped<ILearningCompletionClient, LearningCompletionClient>();
        services.AddScoped<ISchoolSchedulingClient, SchoolSchedulingClient>();
        return services;
    }
}
