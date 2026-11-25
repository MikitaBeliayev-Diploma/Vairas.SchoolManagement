using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Committed local mutation and its current authorized representation.</summary>
public sealed record MutationResponse<T>(Guid OperationId, T Resource, bool Replayed);
