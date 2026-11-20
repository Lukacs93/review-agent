using Microsoft.EntityFrameworkCore;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.Persistence.RunLog;

namespace ReviewAgent.Infrastructure.Persistence;

/// <summary>The application's database. Review runs are stored as an append-only list of events.</summary>
internal sealed class ReviewAgentDbContext(DbContextOptions<ReviewAgentDbContext> options) : DbContext(options)
{
    public DbSet<StoredReviewRun> ReviewRuns => Set<StoredReviewRun>();

    public DbSet<StoredReviewEvent> ReviewEvents => Set<StoredReviewEvent>();

    /// <summary>The log lines of the review runs; see <see cref="Logging.RunLogProvider"/>.</summary>
    public DbSet<StoredRunLogEntry> RunLogEntries => Set<StoredRunLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StoredReviewRunConfiguration());
        modelBuilder.ApplyConfiguration(new StoredReviewEventConfiguration());
        modelBuilder.ApplyConfiguration(new StoredRunLogEntryConfiguration());
    }
}
