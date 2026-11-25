using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolDocumentService { Task<DocumentAuthorityResponse> AuthorityAsync(DocumentAuthorityRequest request, CancellationToken ct); }
