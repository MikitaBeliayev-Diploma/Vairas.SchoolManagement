using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vairas.SchoolManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Configuration.Options;
namespace Vairas.SchoolManagement.Integrations;

public sealed class SchoolCoordinationWorker(IServiceScopeFactory scopes,IOptions<SchedulingDependencyOptions> optionsSettings,ILogger<SchoolCoordinationWorker> logger):BackgroundService
{
    private readonly SchedulingDependencyOptions options = optionsSettings.Value;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Enabled)return;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var queue=scope.ServiceProvider.GetRequiredService<ICoordinationQueue>();
                foreach(var item in await queue.PendingAsync(ct))
                {
                    try{await scope.ServiceProvider.GetRequiredService<ISchoolSchedulingService>().ResumeAsync(item.School,item.Operation,ct);}
                    catch(Exception e)when(!ct.IsCancellationRequested){logger.LogWarning("School coordination recovery failed with {ExceptionType}",e.GetType().Name);}
                }
                await Task.Delay(TimeSpan.FromSeconds(5),ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e){logger.LogError("School coordination queue failed with {ExceptionType}",e.GetType().Name);await Task.Delay(TimeSpan.FromSeconds(5),ct);}
        }
    }
}
