using System.Security.Cryptography;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

public interface IAssistantAccessService { Task<AssistantAccessResponse> ReadAsync(AssistantAccessRequest request, CancellationToken ct); }
