using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Audited coordinated School command with a stable result identity.</summary>
public sealed record SchoolChangeRequest(string Kind,Guid TargetId,long ExpectedRevision=0,Guid? StudentId=null,string? Category=null,Guid? GroupId=null,Guid? ProgramVersionId=null,Guid? TeacherId=null,string? Status=null,Guid? InvitationId=null,string? Permission=null,string? Reason=null,Guid? UserId=null);
