namespace Vairas.SchoolManagement.Business;

public interface ILearningCompletionClient
{
    Task<LearningCompletionEvidence> RequireCurrentCompletionAsync(Guid school, Guid enrollment, CancellationToken ct);
}
