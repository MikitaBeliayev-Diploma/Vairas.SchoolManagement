using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
namespace Vairas.SchoolManagement.Integrations;

/// <summary>
/// One ServiceBusClient (one AMQP connection) per connection string for the whole process, shared by the outbox publisher
/// and the inbox consumer, as the Azure SDK recommends. Workers create and dispose their own senders/processors only.
/// </summary>
public sealed class ServiceBusClients : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<ServiceBusClient>> clients = new(StringComparer.Ordinal);
    public ServiceBusClient Get(string connectionString) =>
        clients.GetOrAdd(connectionString, c => new Lazy<ServiceBusClient>(() => new ServiceBusClient(c))).Value;
    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients.Values.Where(x => x.IsValueCreated)) await client.Value.DisposeAsync();
        clients.Clear();
    }
}
