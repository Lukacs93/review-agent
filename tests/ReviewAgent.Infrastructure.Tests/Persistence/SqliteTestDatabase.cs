using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReviewAgent.Infrastructure.Persistence;

namespace ReviewAgent.Infrastructure.Tests.Persistence;

/// <summary>
/// A real SQLite database kept in memory for one test. It lives as long as the connection is open,
/// so every context created here sees the same data, like separate requests against one database.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();

        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    public ReviewAgentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ReviewAgentDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
