using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Configuration.Options;

namespace Vairas.SchoolManagement.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    public static IServiceCollection AddSchoolOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<DatabaseOptions, DatabaseOptionsValidator>(services, configuration.GetSection("ConnectionStrings"));
        AddValidated<JwtOptions, JwtOptionsValidator>(services, configuration.GetSection("Authentication"));
        AddValidated<DependencyOptions, DependencyOptionsValidator>(services, configuration.GetSection("Dependencies"));
        AddValidated<SchoolAccessOptions, SchoolAccessOptionsValidator>(services, configuration.GetSection("Access"));
        AddValidated<SchoolInboxOptions, SchoolInboxOptionsValidator>(services, configuration.GetSection("Inbox"));
        services.AddOptions<SchedulingDependencyOptions>().Bind(configuration.GetSection("SchedulingDependency"));
        services.AddOptions<SchoolLearningBusOptions>().Bind(configuration.GetSection("LearningServiceBus"));
        services.AddOptions<HostFilteringOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostFilteringOptions>, AllowedHostsValidator>();
        // The business layer takes the plain settings object so it does not depend on the options infrastructure.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SchoolAccessOptions>>().Value);
        return services;
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
