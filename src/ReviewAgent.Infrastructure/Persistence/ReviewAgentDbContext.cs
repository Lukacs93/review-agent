using Microsoft.EntityFrameworkCore;
using ReviewAgent.Infrastructure.Persistence.Reviews;

namespace ReviewAgent.Infrastructure.Persistence;

/// <summary>The application's database. Review runs are stored as an append-only list of events.</summary>
public sealed class ReviewAgentDbContext(DbContextOptions<ReviewAgentDbContext> options) : DbContext(options)
{
    public DbSet<StoredReviewRun> ReviewRuns => Set<StoredReviewRun>();

    public DbSet<StoredReviewEvent> ReviewEvents => Set<StoredReviewEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StoredReviewRunConfiguration());
        modelBuilder.ApplyConfiguration(new StoredReviewEventConfiguration());
    }
}
