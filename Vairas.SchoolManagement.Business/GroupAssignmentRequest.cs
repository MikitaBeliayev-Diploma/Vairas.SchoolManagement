using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Group/programme association requires Learning and Scheduling validation.</summary>
public sealed record GroupAssignmentRequest(Guid GroupId, Guid ProgramVersionId, long ExpectedRevision);
