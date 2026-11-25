using Azure.Messaging.ServiceBus;
using Npgsql;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Configuration.Options;
namespace Vairas.SchoolManagement.Integrations;


public sealed class LearningOutboxWorker(IOptions<DatabaseOptions> database,IOptions<SchoolLearningBusOptions> optionsSettings,ServiceBusClients clients,ILogger<LearningOutboxWorker> logger):BackgroundService
{
    private readonly SchoolLearningBusOptions options = optionsSettings.Value;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Enabled)return;
        var client=clients.Get(options.ConnectionString);
        await using var sender=client.CreateSender(options.TopicName);
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await using var db=new NpgsqlConnection(database.Value.LearningDelivery);await db.OpenAsync(ct);
                var lease=Guid.NewGuid();
                await using var claim=db.CreateCommand();
                claim.CommandText="""
                    WITH c AS(SELECT id FROM school_learning_outbox WHERE sent_at IS NULL AND next_attempt_at<=clock_timestamp()
                    AND(lease_until IS NULL OR lease_until<clock_timestamp()) ORDER BY occurred_at,id FOR UPDATE SKIP LOCKED LIMIT 1)
                    UPDATE school_learning_outbox o SET lease_id=@lease,lease_until=clock_timestamp()+interval '30 seconds',attempts=attempts+1
                    FROM c WHERE o.id=c.id RETURNING o.id,o.aggregate_id,o.aggregate_version,o.event_type,o.payload,o.correlation_id,o.occurred_at
                    """;
                claim.Parameters.AddWithValue("lease",lease);
                Guid id,aggregate;long revision;string type,payload,correlation;DateTimeOffset occurred;
                await using(var reader=await claim.ExecuteReaderAsync(ct))
                {
                    if(!await reader.ReadAsync(ct)){await Task.Delay(TimeSpan.FromSeconds(2),ct);continue;}
                    id=reader.GetGuid(0);aggregate=reader.GetGuid(1);revision=reader.GetInt64(2);type=reader.GetString(3);payload=reader.GetString(4);correlation=reader.GetString(5);occurred=reader.GetFieldValue<DateTimeOffset>(6);
                }
                var sent=false;
                try
                {
                    var m=new ServiceBusMessage(BinaryData.FromString(payload)){MessageId=id.ToString("D"),Subject=type,CorrelationId=correlation,ContentType="application/json"};
                    m.ApplicationProperties["eventId"]=id.ToString("D");m.ApplicationProperties["eventType"]=type;m.ApplicationProperties["eventVersion"]=1;
                    m.ApplicationProperties["occurredAt"]=occurred.ToString("O");m.ApplicationProperties["producer"]="school";m.ApplicationProperties["correlationId"]=correlation;
                    m.ApplicationProperties["aggregateId"]=aggregate.ToString("D");m.ApplicationProperties["aggregateVersion"]=revision;
                    await sender.SendMessageAsync(m,ct);sent=true;
                }
                catch(Exception e)when(!ct.IsCancellationRequested){logger.LogWarning("School learning event send failed: {ExceptionType}",e.GetType().Name);}
                await using var finish=db.CreateCommand();
                finish.CommandText="""
                    UPDATE school_learning_outbox SET sent_at=CASE WHEN @sent THEN clock_timestamp() ELSE sent_at END,
                    next_attempt_at=clock_timestamp()+make_interval(secs=>least(300,power(2,least(attempts,8)))::integer),lease_id=NULL,lease_until=NULL
                    WHERE id=@id AND lease_id=@lease
                    """;
                finish.Parameters.AddWithValue("sent",sent);finish.Parameters.AddWithValue("id",id);finish.Parameters.AddWithValue("lease",lease);await finish.ExecuteNonQueryAsync(ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning("School learning Outbox unavailable: {ExceptionType}",e.GetType().Name);await Task.Delay(TimeSpan.FromSeconds(5),ct);}
        }
    }
}
