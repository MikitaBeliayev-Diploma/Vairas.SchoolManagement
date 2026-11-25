using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Persistance;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        SchoolModel.Configure(builder);
        builder.Entity<SchoolCoordination>().ToTable("school_coordination").HasKey(x=>new{x.OperationId,x.SchoolId});
        var names=new Npgsql.NameTranslation.NpgsqlSnakeCaseNameTranslator();
        foreach(var property in builder.Entity<SchoolCoordination>().Metadata.GetProperties())property.SetColumnName(names.TranslateMemberName(property.Name));
        builder.Entity<SchoolLearningOutbox>().ToTable("school_learning_outbox").HasKey(x=>x.Id);
        foreach(var property in builder.Entity<SchoolLearningOutbox>().Metadata.GetProperties())property.SetColumnName(names.TranslateMemberName(property.Name));
        builder.Entity<SchoolLearningOutbox>().HasIndex(x=>new{x.SentAt,x.NextAttemptAt});
        builder.Entity<MembershipReview>(e=>{e.ToTable("membership_review").HasKey(x=>x.ReviewId);e.HasIndex(x=>new{x.SourceEventId,x.TargetKind,x.TargetId}).IsUnique();e.HasIndex(x=>new{x.SchoolId,x.ResolvedAt});
            e.Property(x=>x.TargetKind).HasMaxLength(32);e.Property(x=>x.ReasonCode).HasMaxLength(64);e.Property(x=>x.SubjectRole).HasMaxLength(16);e.Property(x=>x.ResolutionNote).HasMaxLength(500);});
        builder.Entity<SchoolIntegrationInbox>(e=>{e.ToTable("integration_inbox").HasKey(x=>x.EventId);e.Property(x=>x.Producer).HasMaxLength(32);e.Property(x=>x.EventType).HasMaxLength(64);
            e.Property(x=>x.Fingerprint).HasMaxLength(64);e.Property(x=>x.Outcome).HasMaxLength(32);});
        foreach(var type in new[]{typeof(MembershipReview),typeof(SchoolIntegrationInbox)})
            foreach(var property in builder.Entity(type).Metadata.GetProperties())property.SetColumnName(names.TranslateMemberName(property.Name));
    }
}
