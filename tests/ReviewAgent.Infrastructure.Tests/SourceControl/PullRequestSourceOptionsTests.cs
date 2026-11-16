using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewAgent.Infrastructure.SourceControl;
using ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class PullRequestSourceOptionsTests
{
    private const string SavedDirectoryKey = "PullRequests:SavedDirectory";
    private const string SavedDirectory = "samples/pull-requests";

    [Fact]
    public void Saved_directory_is_read_from_the_configuration()
    {
        using var services = BuildServices(SavedDirectory);

        var options = services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;

        Assert.Equal(SavedDirectory, options.SavedDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_saved_directory_is_rejected(string? savedDirectory)
    {
        using var services = BuildServices(savedDirectory);

        var error = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value);

        // The validator wraps the attribute's message: "... 'SavedDirectory' with the error: '<message>'."
        Assert.Contains(
            error.Failures,
            failure => failure.Contains(SavedPullRequestErrors.DirectoryRequired, StringComparison.Ordinal));
    }

    private static ServiceProvider BuildServices(string? savedDirectory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SavedDirectoryKey] = savedDirectory })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure()
            .BuildServiceProvider();
    }
}
