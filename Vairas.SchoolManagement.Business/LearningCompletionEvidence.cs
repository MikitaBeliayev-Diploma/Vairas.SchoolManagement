namespace Vairas.SchoolManagement.Business;

/// <summary>Exact current Learning evidence retained with the School completion history.</summary>
public sealed record LearningCompletionEvidence(Guid SnapshotId,long DependencyRevision,DateTimeOffset VerifiedAt,DateTimeOffset ValidUntil);
