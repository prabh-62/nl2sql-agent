using System.ClientModel.Primitives;
using System.Data.Common;
using Azure.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Nl2Sql.Agent;

/// <summary>How the model learns the schema.</summary>
public enum CatalogMode
{
    /// <summary>The model calls list_views and describe_view to discover the views it needs.</summary>
    Discovery,

    /// <summary>Every view, column and description is in the system prompt; no discovery tools.</summary>
    Prompt,
}

public sealed class AgentOptions
{
    /// <summary>Model round trips per question; the last one can't call tools, so it must answer.</summary>
    public int MaxIterations { get; set; } = 5;

    public int MaxOutputTokens { get; set; } = 2000;

    public CatalogMode CatalogMode { get; set; } = CatalogMode.Discovery;
}

/// <summary>
/// Builds the agent the same way for the API and the evals, so the evals measure the code that ships.
/// </summary>
public static class AgentFactory
{
    /// <summary>
    /// A chat client for a Foundry model deployment, using the Responses API (reasoning models such as
    /// gpt-6-luna only accept function tools there) and Entra sign-in (key auth is disabled).
    /// </summary>
    /// <param name="foundryConnectionString">"Endpoint=https://&lt;resource&gt;.cognitiveservices.azure.com/;Deployment=&lt;name&gt;".</param>
    public static IChatClient CreateFoundryChatClient(string foundryConnectionString, TokenCredential credential, string? deploymentOverride = null)
    {
        var settings = new DbConnectionStringBuilder { ConnectionString = foundryConnectionString };
        var endpoint = new Uri(new Uri((string)settings["Endpoint"]), "openai/v1/");
        var deployment = deploymentOverride ?? (string)settings["Deployment"];
        var authentication = new BearerTokenPolicy(credential, "https://cognitiveservices.azure.com/.default");

#pragma warning disable OPENAI001 // Token-credential constructor and Responses client are marked experimental in the OpenAI SDK.
        var client = new OpenAIClient(authentication, new OpenAIClientOptions { Endpoint = endpoint });
        return client.GetResponsesClient().AsIChatClient(deployment);
#pragma warning restore OPENAI001
    }

    /// <summary>The model wrapped with our context, limits, call counting and telemetry, outermost first.</summary>
    public static IChatClient BuildPipeline(
        IChatClient model,
        IAgentRunAccessor runs,
        TimeProvider time,
        AgentOptions options,
        string telemetrySource,
        bool recordSensitiveData,
        IServiceProvider? services = null) =>
        model.AsBuilder()
            // Who the user is and today's date, once per question.
            .Use(inner => new AgentContextChatClient(inner, runs, time))
            .UseFunctionInvocation(configure: invoker => invoker.MaximumIterationsPerRequest = options.MaxIterations)
            .UseOpenTelemetry(sourceName: telemetrySource, configure: telemetry => telemetry.EnableSensitiveData = recordSensitiveData)
            // Innermost: one count per actual call to the model.
            .Use(inner => new ModelCallCounter(inner, runs))
            .Build(services);

    public static async Task<ChatClientAgent> CreateAsync(
        IChatClient pipeline,
        AgentTools tools,
        AgentOptions options,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null,
        CancellationToken cancellationToken = default)
    {
        var catalog = options.CatalogMode == CatalogMode.Prompt ? await tools.DescribeCatalogAsync(cancellationToken) : null;

        List<AITool> agentTools = options.CatalogMode == CatalogMode.Discovery
            ?
            [
                AIFunctionFactory.Create(tools.ListViewsAsync, name: "list_views"),
                AIFunctionFactory.Create(tools.DescribeViewAsync, name: "describe_view"),
            ]
            : [];
        agentTools.Add(AIFunctionFactory.Create(tools.GetExamplesAsync, name: "get_examples"));
        agentTools.Add(AIFunctionFactory.Create(tools.RunQueryAsync, name: "run_query"));

        return new ChatClientAgent(
            pipeline,
            new ChatClientAgentOptions
            {
                Name = "nl2sql",
                // The pipeline already has its own function-invocation loop with our limits.
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = Instructions(catalog),
                    MaxOutputTokens = options.MaxOutputTokens,
                    Tools = agentTools,
                },
            },
            loggerFactory,
            services);
    }

    /// <summary>The system instructions; with a catalog, it replaces the discovery steps.</summary>
    public static string Instructions(string? catalog)
    {
        var schemaRule = catalog is null
            ? """
              - Only the views in schema ai exist for you. Call list_views, then describe_view for the views
                you need. Never guess column names.
              - Call describe_view and get_examples in the same turn to save time.
              """
            : """
              - Only the views in schema ai exist for you; they're all described below. Use only these columns.
              """;

        var instructions = $"""
            You answer questions from café owners and managers about their coffee-ordering business by
            querying a SQL Server database (T-SQL).

            {schemaRule.TrimEnd()}
            - Call get_examples with the user's question and follow the patterns it returns, especially the
              business rules in their notes (net revenue, local time, what counts as a latte or a customer).
            - Answer with run_query: one SELECT, two-part names (ai.Orders). If it returns an error, fix the
              query and try again; you have 2 retries.
            - Pass a chart to run_query when it helps: line for a trend over time, bar to compare a few
              categories. Order the rows the way they should be plotted. No chart for single numbers.
            - The data is already limited to what this user may see. Never filter by brand or location
              unless the user asks about a specific location.
            - Answer in plain language in 2 to 4 sentences, with the few numbers that matter (short markdown
              is fine). The user sees the full table and the chart under your answer, so never list every row.
            - If the question isn't about the data, or asks you to change data, say you can only answer
              questions about it.
            """;

        return catalog is null ? instructions : $"{instructions}\n\n## The ai views\n\n{catalog}";
    }

    private sealed class ModelCallCounter(IChatClient inner, IAgentRunAccessor runs) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Count();
            return base.GetResponseAsync(messages, options, cancellationToken);
        }

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Count();
            return base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        private void Count()
        {
            if (runs.Current is { } run)
            {
                run.ModelCalls++;
            }
        }
    }
}
