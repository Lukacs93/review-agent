using System.ComponentModel.DataAnnotations;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.Errors;

namespace ReviewAgent.Infrastructure.Tests.Agents;

public sealed class AgentOptionsTests
{
    [Fact]
    public void Default_timeouts_are_valid()
    {
        Assert.Empty(Validate(new AgentOptions
        {
            Endpoint = new Uri("https://model.example/"),
            Model = "test-model",
            McpServerUrl = new Uri("http://localhost:5090/"),
        }));
    }

    [Fact]
    public void Request_timeout_must_be_shorter_than_the_review_timeout()
    {
        var errors = Validate(new AgentOptions
        {
            Endpoint = new Uri("https://model.example/"),
            Model = "test-model",
            McpServerUrl = new Uri("http://localhost:5090/"),
            RequestTimeout = TimeSpan.FromMinutes(5),
            Timeout = TimeSpan.FromMinutes(5),
        });

        Assert.Equal(AgentErrors.RequestTimeoutNotShorter, Assert.Single(errors).ErrorMessage);
    }

    [Fact]
    public void Missing_model_settings_are_reported()
    {
        var messages = Validate(new AgentOptions()).Select(error => error.ErrorMessage);

        Assert.Contains(AgentErrors.EndpointRequired, messages);
        Assert.Contains(AgentErrors.ModelRequired, messages);
        Assert.Contains(AgentErrors.McpServerUrlRequired, messages);
    }

    [Fact]
    public void Requests_per_minute_must_allow_at_least_one_request()
    {
        var errors = Validate(new AgentOptions
        {
            Endpoint = new Uri("https://model.example/"),
            Model = "test-model",
            McpServerUrl = new Uri("http://localhost:5090/"),
            RequestsPerMinute = 0,
        });

        Assert.Equal(AgentErrors.RequestsPerMinuteOutOfRange, Assert.Single(errors).ErrorMessage);
    }

    // The same check ValidateDataAnnotations runs at startup.
    private static List<ValidationResult> Validate(AgentOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
