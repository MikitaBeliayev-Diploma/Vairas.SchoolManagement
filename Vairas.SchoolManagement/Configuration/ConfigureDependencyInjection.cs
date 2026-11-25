using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;

namespace Vairas.SchoolManagement.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddSchoolServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISchoolDocumentService, SchoolDocumentService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IMembershipReviewService, SchoolService>();
        services.AddScoped<IAssistantAccessService, AssistantAccessService>();
        services.AddScoped<INotificationRecipientsService, NotificationRecipientsService>();
        services.AddScoped<ISchoolLearningService, SchoolLearningService>();
        services.AddScoped<ISchoolSchedulingService, SchoolSchedulingService>();
        services.AddScoped<ICoordinationQueue, CoordinationQueue>();
        services.AddScoped<ISchoolActorFactory, SchoolActorFactory>();
        return services;
    }
}
