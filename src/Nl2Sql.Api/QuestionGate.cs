using System.Text.Json;
using Nl2Sql.Agent;

namespace Nl2Sql.Api;

/// <summary>
/// Runs before every question (POST to the AG-UI endpoint), cheapest check first: a known persona,
/// a well-formed request with a short enough question, then the quota. Only questions that pass all
/// three reach the model, so bad requests never cost tokens or quota.
/// </summary>
public sealed class QuestionGate(RequestDelegate next, QuestionQuota quota, ILogger<QuestionGate> logger)
{
    public const int MaxQuestionLength = 500;

    private const int MaxBodyBytes = 64 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        var persona = Personas.Find(context.Request.Headers[Personas.HeaderName]);
        if (persona is null)
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, "unknown_persona", "Choose one of the demo personas.");
            return;
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, "too_long", "The conversation is too long. Start a new one.");
            return;
        }

        context.Request.EnableBuffering(bufferThreshold: MaxBodyBytes, bufferLimit: MaxBodyBytes);
        var question = await ReadLastUserMessageAsync(context);
        context.Request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(question))
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, "no_question", "Ask a question.");
            return;
        }

        if (question.Length > MaxQuestionLength)
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, "too_long", $"Questions are limited to {MaxQuestionLength} characters.");
            return;
        }

        var client = ClientKey(context);
        switch (quota.TryConsume(client))
        {
            case QuotaVerdict.ClientLimitReached:
                await RejectAsync(context, StatusCodes.Status429TooManyRequests, "client_limit",
                    $"You've used your {quota.Options.QuestionsPerClientPerDay} questions for today. Come back tomorrow.");
                return;
            case QuotaVerdict.DemoPaused:
                await RejectAsync(context, StatusCodes.Status429TooManyRequests, "demo_paused", "The demo is paused for today. Come back tomorrow.");
                return;
        }

        context.SetAgentRun(new AgentRun(persona));
        Log.Question(logger, persona.Id, question.Length);
        await next(context);
    }

    /// <summary>The client's IP. Behind App Service, the forwarded-headers middleware has already put the real one here.</summary>
    public static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async Task<string?> ReadLastUserMessageAsync(HttpContext context)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            if (!document.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return messages.EnumerateArray()
                .Where(m => m.TryGetProperty("role", out var role) && role.ValueEquals("user"))
                .Select(m => m.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() : null)
                .LastOrDefault();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Task RejectAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { error = code, message });
    }
}

public static class AgentRunHttpExtensions
{
    private static readonly object Key = new();

    public static void SetAgentRun(this HttpContext context, AgentRun run) => context.Items[Key] = run;

    public static AgentRun? GetAgentRun(this HttpContext context) => context.Items[Key] as AgentRun;
}

/// <summary>In the API, a question is one HTTP request, so its <see cref="AgentRun"/> lives on the request.</summary>
public sealed class HttpAgentRunAccessor(IHttpContextAccessor httpContextAccessor) : IAgentRunAccessor
{
    public AgentRun? Current => httpContextAccessor.HttpContext?.GetAgentRun();
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Question from persona {Persona}, {Length} characters.")]
    public static partial void Question(ILogger logger, string persona, int length);
}
