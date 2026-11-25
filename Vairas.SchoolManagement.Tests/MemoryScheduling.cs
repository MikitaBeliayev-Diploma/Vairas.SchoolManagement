using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

internal sealed class MemoryScheduling:ISchoolSchedulingClient
{
    public bool Enabled{get;set;}=true;public bool FailFinish;public bool FailPrepare;public bool RejectPrepare;public int Preparations,Finishes;
    public Dictionary<Guid,SchedulingTransition> Transitions=[];public SchedulingImpact[] Impacts=[];
    public Task<SchedulingTransitionOutcome> PrepareAsync(SchedulingTransition r,CancellationToken ct)
    {Preparations++;if(RejectPrepare)throw SchoolErrors.Conflict();Transitions[r.OperationId]=r;if(FailPrepare)throw SchoolErrors.Dependency();return Task.FromResult(new SchedulingTransitionOutcome(r.OperationId,r.SchoolId,"Prepared",r.AfterRevision));}
    public Task<SchedulingTransitionOutcome> FinishAsync(Guid school,Guid operation,string decision,CancellationToken ct)
    {Finishes++;if(FailFinish)throw SchoolErrors.Dependency();return Task.FromResult(new SchedulingTransitionOutcome(operation,school,decision=="abort"?"Aborted":"Committed",Transitions[operation].AfterRevision));}
    public Task<IReadOnlyList<SchedulingImpact>> ImpactsAsync(Guid school,string kind,Guid target,DateTimeOffset from,CancellationToken ct)=>Task.FromResult<IReadOnlyList<SchedulingImpact>>(Impacts);
    public Task ValidateProgrammeAsync(Guid school,Guid enrollment,Guid group,Guid programme,CancellationToken ct)=>Task.CompletedTask;
}
