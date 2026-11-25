using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Integrations;

public interface ICoordinationQueue {Task<IReadOnlyList<(Guid School,Guid Operation)>> PendingAsync(CancellationToken ct);}
