using Microsoft.Extensions.AI;

namespace Nl2Sql.Agent;

/// <summary>
/// Tells the model, on every request, who it's answering for and what "today" is in their time zone.
/// Without the date, the model assumes the year from its training data ("September" became 2024).
/// </summary>
public sealed class AgentContextChatClient(IChatClient inner, IAgentRunAccessor runs, TimeProvider time)
    : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(WithContext(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(WithContext(messages), options, cancellationToken);

    private IEnumerable<ChatMessage> WithContext(IEnumerable<ChatMessage> messages)
    {
        var persona = runs.Current?.Persona
            ?? throw new InvalidOperationException("No question is being answered.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(persona.TimeZone);
        var now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);

        var context = new ChatMessage(ChatRole.System, $"""
            You are answering for: {persona.Name}. {persona.Description}
            Today is {now:dddd, MMMM d, yyyy} ({now:yyyy-MM-dd}), {persona.TimeZone}. Use this date for "today",
            "last week", "last month", "this quarter" and for any month or season without a year.
            """);
        return [context, .. messages];
    }
}
