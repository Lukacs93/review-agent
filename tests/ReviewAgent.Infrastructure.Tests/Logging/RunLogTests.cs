using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewAgent.Infrastructure.Logging;
using ReviewAgent.Infrastructure.Persistence.RunLog;
using ReviewAgent.Infrastructure.Tests.Persistence;

namespace ReviewAgent.Infrastructure.Tests.Logging;

public sealed class RunLogTests : IDisposable
{
    private const string Category = "ReviewAgent.Infrastructure.Agents.ReviewAgentRunner";

    private static readonly Func<ILogger, Guid, IDisposable?> _runScope =
        LoggerMessage.DefineScope<Guid>("Review run {" + RunLogProvider.RunIdKey + "}");

    // Defined once, like the log messages of the application code (CA1848).
    private static readonly Action<ILogger, Exception?> _logModelSlow =
        LoggerMessage.Define(LogLevel.Warning, default, "The model is slow.");

    private static readonly Action<ILogger, Exception?> _logStarted =
        LoggerMessage.Define(LogLevel.Warning, default, "The application started.");

    private readonly SqliteTestDatabase _database = new();
    private readonly RunLogQueue _queue = new(Options.Create(new RunLogOptions()));

    [Fact]
    public void Lines_inside_a_review_run_are_kept_with_its_id()
    {
        var runId = Guid.NewGuid();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(Provider()));
        var logger = loggerFactory.CreateLogger(Category);

        using (_runScope(logger, runId))
        {
            _logModelSlow(logger, null);
        }

        Assert.True(_queue.Reader.TryRead(out var line));
        Assert.Equal(runId, line.RunId);
        Assert.Equal("Warning", line.Level);
        Assert.Equal(Category, line.Category);
        Assert.Equal("The model is slow.", line.Message);
    }

    [Fact]
    public void Lines_outside_a_review_run_are_left_out()
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(Provider()));

        _logStarted(loggerFactory.CreateLogger(Category), null);

        Assert.False(_queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Waiting_lines_are_saved_and_read_back_in_order()
    {
        var runId = Guid.NewGuid();
        _queue.Add(Line(runId, "First."));
        _queue.Add(Line(Guid.NewGuid(), "Another run."));
        _queue.Add(Line(runId, "Second."));
        await using var services = new ServiceCollection()
            .AddScoped(_ => _database.CreateDbContext())
            .BuildServiceProvider();

        var writer = new RunLogWriter(
            _queue,
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RunLogWriter>.Instance);

        await writer.SaveWaitingLinesAsync(CancellationToken.None);

        await using var dbContext = _database.CreateDbContext();
        var lines = await new EfRunLogReader(dbContext).ReadAsync(runId, CancellationToken.None);
        string[] expected = ["First.", "Second."];
        Assert.Equal(expected, lines.Select(line => line.Message));
    }

    public void Dispose() => _database.Dispose();

    private RunLogProvider Provider() => new(_queue, TimeProvider.System);

    private static StoredRunLogEntry Line(Guid runId, string message) => new()
    {
        RunId = runId,
        Timestamp = DateTimeOffset.UtcNow,
        Level = "Information",
        Category = Category,
        Message = message,
    };
}
