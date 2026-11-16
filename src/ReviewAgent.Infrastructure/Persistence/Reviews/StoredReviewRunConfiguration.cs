using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReviewAgent.Infrastructure.Persistence.Reviews;

internal sealed class StoredReviewRunConfiguration : IEntityTypeConfiguration<StoredReviewRun>
{
    public void Configure(EntityTypeBuilder<StoredReviewRun> builder)
    {
        builder.ToTable("ReviewRuns");

        builder.HasKey(storedRun => storedRun.Id);

        // EF Core adds the loaded value to the WHERE clause of the UPDATE; if it changed meanwhile, no row matches.
        builder.Property(storedRun => storedRun.Version).IsConcurrencyToken();
    }
}
