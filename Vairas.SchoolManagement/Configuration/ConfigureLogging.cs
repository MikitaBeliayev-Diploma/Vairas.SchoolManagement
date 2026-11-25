namespace Vairas.SchoolManagement.Configuration;

public static class ConfigureLogging
{
    public static ILoggingBuilder AddJsonLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options => options.IncludeScopes = true);
        return logging;
    }
}
