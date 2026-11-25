using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;

namespace Vairas.SchoolManagement.Controllers;

[Route("internal/v1/notifications/recipients"), Authorize(Policy = "school.notifications.read")]
public sealed class NotificationRecipientsController(INotificationRecipientsService service) : BaseApiController
{
    /// <summary>Resolve current permitted recipients for a trusted lesson or assessment source.</summary>
    [HttpPost(Name = "resolveNotificationRecipients")]
    public Task<NotificationRecipientsResponse> Resolve(NotificationRecipientsRequest request, CancellationToken ct) => service.ResolveAsync(request, ct);
}
