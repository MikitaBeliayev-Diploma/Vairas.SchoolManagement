using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Configuration.Options;
using Vairas.SchoolManagement.Persistance;

namespace Vairas.SchoolManagement.Configuration;

public static class ConfigureDatabase
{
    public static IServiceCollection AddSchoolDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<SchoolDbContext>((provider, builder) =>
            SchoolDatabase.Configure(builder, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.SchoolManagement));
        services.AddScoped<ISchoolStore, SchoolStore>();
        return services;
    }
}
