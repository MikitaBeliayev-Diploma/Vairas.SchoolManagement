using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

public interface INotificationRecipientsService { Task<NotificationRecipientsResponse> ResolveAsync(NotificationRecipientsRequest request, CancellationToken ct); }
