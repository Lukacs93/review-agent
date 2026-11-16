using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReviewAgent.Infrastructure.Persistence.Reviews;

internal sealed class StoredReviewEventConfiguration : IEntityTypeConfiguration<StoredReviewEvent>
{
    private const int EventTypeMaxLength = 100;

    public void Configure(EntityTypeBuilder<StoredReviewEvent> builder)
    {
        builder.ToTable("ReviewEvents");

        // The run's version check prevents duplicate versions; this key is a safety net behind it.
        builder.HasKey(storedEvent => new { storedEvent.RunId, storedEvent.Version });

        // Events are the run's history: deleting a run must not silently delete them.
        builder.HasOne<StoredReviewRun>()
            .WithMany()
            .HasForeignKey(storedEvent => storedEvent.RunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(storedEvent => storedEvent.EventType).HasMaxLength(EventTypeMaxLength);
    }
}
