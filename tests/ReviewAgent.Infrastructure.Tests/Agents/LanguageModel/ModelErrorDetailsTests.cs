using ReviewAgent.Infrastructure.Agents.LanguageModel;

namespace ReviewAgent.Infrastructure.Tests.Agents.LanguageModel;

public sealed class ModelErrorDetailsTests
{
    private const string ApiMessage = "Function call is missing a thought_signature.";

    [Fact]
    public void Message_is_read_from_an_OpenAI_compatible_error_answer()
    {
        var answer = $$"""{ "error": { "message": "{{ApiMessage}}", "type": "invalid_request_error" } }""";

        Assert.Equal(ApiMessage, ModelErrorDetails.MessageOf(answer));
    }

    [Fact]
    public void Message_is_read_from_an_error_answer_in_a_list()
    {
        var answer = $$"""[{ "error": { "code": 400, "message": "{{ApiMessage}}", "status": "INVALID_ARGUMENT" } }]""";

        Assert.Equal(ApiMessage, ModelErrorDetails.MessageOf(answer));
    }

    [Theory]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("""{ "detail": "Not an OpenAI-compatible error." }""")]
    [InlineData("""{ "error": { "message": " " } }""")]
    [InlineData("")]
    public void Answer_without_an_error_message_gives_none(string answer)
    {
        Assert.Null(ModelErrorDetails.MessageOf(answer));
    }

    [Fact]
    public void Exception_without_an_answer_is_described_by_its_message()
    {
        var exception = new InvalidOperationException("The model is not available.");

        Assert.Equal("The model is not available.", ModelErrorDetails.Describe(exception));
        Assert.Null(ModelErrorDetails.AnswerOf(exception));
    }
}
