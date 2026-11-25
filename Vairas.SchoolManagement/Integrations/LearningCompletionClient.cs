using Vairas.SchoolManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Configuration.Options;
namespace Vairas.SchoolManagement.Integrations;

public sealed class LearningCompletionClient(IDependencyTransport transport, IOptions<SchedulingDependencyOptions> optionsSettings,TimeProvider clock) : ILearningCompletionClient
{
    private readonly SchedulingDependencyOptions options = optionsSettings.Value;
    public async Task<LearningCompletionEvidence> RequireCurrentCompletionAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        if(!options.Enabled || !Uri.TryCreate(options.LearningBaseUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https")throw SchoolErrors.Dependency();
        var proof=await transport.SendAsync<CompletionProof>(options.LearningBaseUrl,$"internal/v1/student-trainings/{enrollment:D}/completion-evidence?schoolId={school:D}",null,null,ct);
        if(proof.SchoolId!=school || proof.EnrollmentId!=enrollment || proof.SnapshotId==Guid.Empty || proof.DependencyRevision<1 || !proof.Current || proof.Status!="Ready" || proof.Kind!="CompletionReport")throw SchoolErrors.Conflict("LEARNING_COMPLETION_NOT_READY");
        var now=clock.GetUtcNow();return new(proof.SnapshotId,proof.DependencyRevision,now,now.AddSeconds(20));
    }
    private sealed record CompletionProof(Guid SnapshotId,Guid SchoolId,Guid EnrollmentId,long DependencyRevision,string Kind,string Status,bool Current);
}
