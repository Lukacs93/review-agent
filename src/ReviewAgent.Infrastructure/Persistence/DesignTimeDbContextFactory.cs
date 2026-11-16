using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReviewAgent.Infrastructure.Persistence;

/// <summary>Lets the <c>dotnet ef</c> tool create the context when it adds a migration. Not used at run time.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReviewAgentDbContext>
{
    public ReviewAgentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReviewAgentDbContext>()
            .UseSqlite("Data Source=review-agent.db")
            .Options;

        return new ReviewAgentDbContext(options);
    }
}
