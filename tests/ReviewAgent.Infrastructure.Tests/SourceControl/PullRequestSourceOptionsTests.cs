using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.SourceControl;
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
        using var services = BuildServices("other/pull-requests");

        var options = services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;

        Assert.Equal("other/pull-requests", options.SavedDirectory);
    }

    [Fact]
    public void Saved_directory_defaults_to_the_copied_samples()
    {
        using var services = BuildServices(savedDirectory: null);

        var options = services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;

        Assert.Equal(SavedDirectory, options.SavedDirectory);
    }

    [Fact]
    public void Team_rules_path_defaults_to_the_review_folder()
    {
        using var services = BuildServices(savedDirectory: null);

        var options = services.GetRequiredService<IOptions<TeamRulesOptions>>().Value;

        Assert.Equal(".review/rules.md", options.TeamRulesPath);
    }

    [Theory]
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
        // Without a value the key is left out: a key with a null value would still be bound, as an empty text.
        var settings = new Dictionary<string, string?>();
        if (savedDirectory is not null)
        {
            settings[SavedDirectoryKey] = savedDirectory;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure()
            .BuildServiceProvider();
    }
}
