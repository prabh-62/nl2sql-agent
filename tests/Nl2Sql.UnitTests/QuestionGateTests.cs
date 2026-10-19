using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Nl2Sql.Api;

namespace Nl2Sql.UnitTests;

public sealed class QuestionGateTests
{
    [Fact]
    public async Task Lets_a_valid_question_through_with_its_persona()
    {
        var (gate, nextCalled) = Gate();
        var context = Request("maple-queen-st", "Busiest hour last week?");

        await gate.InvokeAsync(context);

        Assert.True(nextCalled());
        Assert.Equal("maple-queen-st", context.GetPersona().Id);
        Assert.Equal(0, context.Request.Body.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("MAPLE-OWNER")]
    public async Task Rejects_unknown_personas(string? persona)
    {
        var (gate, nextCalled) = Gate();
        var context = Request(persona, "Hi");

        await gate.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("unknown_persona", await ErrorCode(context));
    }

    [Fact]
    public async Task Rejects_questions_over_500_characters()
    {
        var (gate, nextCalled) = Gate();
        var context = Request("maple-owner", new string('x', QuestionGate.MaxQuestionLength + 1));

        await gate.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal("too_long", await ErrorCode(context));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"messages":[]}""")]
    [InlineData("""{"messages":[{"role":"assistant","content":"hi"}]}""")]
    public async Task Rejects_requests_without_a_question(string body)
    {
        var (gate, nextCalled) = Gate();
        var context = Request("maple-owner", rawBody: body);

        await gate.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal("no_question", await ErrorCode(context));
    }

    [Fact]
    public async Task Rejected_questions_do_not_use_up_the_quota()
    {
        var quota = new QuestionQuota(new QuotaOptions { QuestionsPerClientPerDay = 1, QuestionsPerDay = 10 });
        var (gate, _) = Gate(quota);

        await gate.InvokeAsync(Request("nobody", "Hi"));
        await gate.InvokeAsync(Request("maple-owner", new string('x', 501)));

        Assert.Equal(1, quota.Remaining("10.0.0.1").Remaining);
    }

    [Fact]
    public async Task Stops_a_client_at_its_daily_limit()
    {
        var quota = new QuestionQuota(new QuotaOptions { QuestionsPerClientPerDay = 2, QuestionsPerDay = 10 });
        var (gate, _) = Gate(quota);

        await gate.InvokeAsync(Request("maple-owner", "One"));
        await gate.InvokeAsync(Request("maple-owner", "Two"));
        var third = Request("maple-owner", "Three");
        await gate.InvokeAsync(third);

        Assert.Equal(StatusCodes.Status429TooManyRequests, third.Response.StatusCode);
        Assert.Equal("client_limit", await ErrorCode(third));
    }

    [Fact]
    public async Task Pauses_the_demo_at_the_global_limit()
    {
        var quota = new QuestionQuota(new QuotaOptions { QuestionsPerClientPerDay = 10, QuestionsPerDay = 2 });
        var (gate, _) = Gate(quota);

        await gate.InvokeAsync(Request("maple-owner", "One", ip: "10.0.0.1"));
        await gate.InvokeAsync(Request("maple-owner", "Two", ip: "10.0.0.2"));
        var third = Request("maple-owner", "Three", ip: "10.0.0.3");
        await gate.InvokeAsync(third);

        Assert.Equal("demo_paused", await ErrorCode(third));
        Assert.Equal(10, quota.Remaining("10.0.0.3").Remaining);
        Assert.True(quota.Remaining("10.0.0.3").DemoPaused);
    }

    [Fact]
    public async Task Each_client_has_its_own_limit()
    {
        var quota = new QuestionQuota(new QuotaOptions { QuestionsPerClientPerDay = 1, QuestionsPerDay = 10 });
        var (gate, _) = Gate(quota);

        await gate.InvokeAsync(Request("maple-owner", "One", ip: "10.0.0.1"));
        var other = Request("maple-owner", "One", ip: "10.0.0.2");
        await gate.InvokeAsync(other);

        Assert.Equal(StatusCodes.Status200OK, other.Response.StatusCode);
        Assert.Equal(0, quota.Remaining("10.0.0.1").Remaining);
    }

    private static (QuestionGate Gate, Func<bool> NextCalled) Gate(QuestionQuota? quota = null)
    {
        var called = false;
        var gate = new QuestionGate(_ => { called = true; return Task.CompletedTask; },
            quota ?? new QuestionQuota(new QuotaOptions()), NullLogger<QuestionGate>.Instance);
        return (gate, () => called);
    }

    private static DefaultHttpContext Request(string? persona, string? question = null, string? rawBody = null, string ip = "10.0.0.1")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/agui";
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (persona is not null)
        {
            context.Request.Headers[Personas.HeaderName] = persona;
        }

        var body = rawBody ?? JsonSerializer.Serialize(new
        {
            threadId = "t",
            runId = "r",
            messages = new[] { new { id = "m1", role = "user", content = question } },
        });
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string?> ErrorCode(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("error").GetString();
    }
}
