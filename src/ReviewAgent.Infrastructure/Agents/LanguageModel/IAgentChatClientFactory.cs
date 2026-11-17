using Microsoft.Extensions.AI;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Creates the client the agent talks to the model with, from the <c>Agent</c> section of the configuration.
/// </summary>
internal interface IAgentChatClientFactory
{
    IChatClient Create();
}
