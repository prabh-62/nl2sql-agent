using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Nl2Sql.Api;

namespace Nl2Sql.UnitTests;

public sealed class AgentContextChatClientTests
{
    [Fact]
    public async Task Tells_the_model_the_persona_and_todays_date_in_their_time_zone()
    {
        var inner = new RecordingChatClient();
        var http = new DefaultHttpContext();
        http.SetPersona(Personas.Find("harbour-owner")!);
        // 06:00 UTC on Oct 20 is still Oct 19 in Vancouver.
        var client = new AgentContextChatClient(inner, new HttpContextAccessor { HttpContext = http }, new FixedTime(new DateTimeOffset(2026, 10, 20, 6, 0, 0, TimeSpan.Zero)));

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Revenue last month?")], cancellationToken: TestContext.Current.CancellationToken);

        var first = inner.Messages![0];
        Assert.Equal(ChatRole.System, first.Role);
        Assert.Contains("Harbour Roast owner", first.Text, StringComparison.Ordinal);
        Assert.Contains("2026-10-19", first.Text, StringComparison.Ordinal);
        Assert.Equal("Revenue last month?", inner.Messages[1].Text);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<ChatMessage>? Messages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Messages = [.. messages];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
