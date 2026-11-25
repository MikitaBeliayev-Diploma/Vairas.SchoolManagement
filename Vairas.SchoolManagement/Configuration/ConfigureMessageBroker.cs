using Vairas.SchoolManagement.Integrations;

namespace Vairas.SchoolManagement.Configuration;

public static class ConfigureMessageBroker
{
    public static IServiceCollection AddMessageBroker(this IServiceCollection services)
    {
        services.AddSingleton<ServiceBusClients>();
        services.AddHostedService<SchoolCoordinationWorker>();
        services.AddHostedService<LearningOutboxWorker>();
        services.AddHostedService<SchoolInboxWorker>();
        return services;
    }
}
