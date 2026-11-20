using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReviewAgent.Infrastructure.Persistence.RunLog;

internal sealed class StoredRunLogEntryConfiguration : IEntityTypeConfiguration<StoredRunLogEntry>
{
    public void Configure(EntityTypeBuilder<StoredRunLogEntry> builder)
    {
        builder.ToTable("RunLogEntries");
        builder.HasKey(entry => entry.Id);

        // A run's lines are always read together.
        builder.HasIndex(entry => entry.RunId);
    }
}
