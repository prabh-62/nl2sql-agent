using System.ComponentModel;
using System.Text.Json;
using Nl2Sql.Data;

namespace Nl2Sql.Api;

/// <summary>
/// The tools the agent can call. Each opens a reader connection scoped to the current request's
/// persona, so the model only ever sees what that tenant's reader can see.
/// </summary>
public sealed class AgentTools(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Failed queries allowed per question before the model must give up and say so.</summary>
    public const int MaxFailedQueries = 2;

    private static readonly object FailedQueriesKey = new();

    // The model sees at most this many rows; the full result (up to QueryRunner.MaxRows) is for the UI.
    private const int RowsForModel = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly string _connectionString = configuration.GetConnectionString("db")
        ?? throw new InvalidOperationException("Connection string 'db' is missing.");

    private readonly string? _impersonateUser = configuration["Database:ImpersonateUser"];

    [Description("Lists the views you can query, with what each one contains. Start here.")]
    public async Task<string> ListViewsAsync(CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(cancellationToken);
        var views = await Catalog.ListViewsAsync(session, cancellationToken);
        return JsonSerializer.Serialize(views, Json);
    }

    [Description("Describes one view: its columns, their types and what they mean. Read this before writing a query against the view.")]
    public async Task<string> DescribeViewAsync(
        [Description("View name, for example ai.Orders.")] string viewName,
        CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(cancellationToken);
        var columns = await Catalog.DescribeViewAsync(session, viewName, cancellationToken);
        return columns is null
            ? $"There is no view named '{viewName}'. Call list_views to see the available views."
            : JsonSerializer.Serialize(columns, Json);
    }

    [Description("Returns worked examples of questions similar to the user's, with the SQL that answers them. Use them as patterns.")]
    public async Task<string> GetExamplesAsync(
        [Description("The user's question.")] string question,
        CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(cancellationToken);
        var examples = await Catalog.GetExamplesAsync(session, question, count: 3, cancellationToken);
        return JsonSerializer.Serialize(examples, Json);
    }

    [Description("Runs one T-SQL SELECT against the ai views and returns the result. If it fails, read the error, fix the query and try again; you have 2 retries per question. When a chart would help the user (a trend over time, or a comparison across a few categories), pass one; skip it for single numbers and long lists.")]
    public async Task<string> RunQueryAsync(
        [Description("A single T-SQL SELECT statement that reads only views in schema ai.")] string sql,
        [Description("Optional chart of this query's result.")] ChartSuggestion? chart,
        CancellationToken cancellationToken)
    {
        var request = Request;
        var failures = request.Items[FailedQueriesKey] as int? ?? 0;
        if (failures >= MaxFailedQueries)
        {
            return JsonSerializer.Serialize(new { error = "No retries left. Tell the user you couldn't answer this question, and why." }, Json);
        }

        await using var session = await OpenAsync(cancellationToken);
        var result = await QueryRunner.RunAsync(session, sql, cancellationToken);
        if (!result.Succeeded)
        {
            request.Items[FailedQueriesKey] = failures + 1;
            return JsonSerializer.Serialize(new { error = result.Error, retriesLeft = MaxFailedQueries - failures - 1 }, Json);
        }

        var rows = result.Rows.Take(RowsForModel).ToArray();

        // Checked here, in the same round trip as the query, so a chart costs no extra model call.
        var chartError = chart?.Validate(result.Columns, rows);

        return JsonSerializer.Serialize(new
        {
            columns = result.Columns,
            rows,
            rowCount = result.Rows.Count,
            truncated = result.IsTruncated || result.Rows.Count > RowsForModel,
            views = result.Views,
            chart = chartError is null ? chart : null,
            chartError,
        }, Json);
    }

    private HttpContext Request => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("Tools only run inside a request.");

    private Task<ReaderSession> OpenAsync(CancellationToken cancellationToken) =>
        ReaderConnection.OpenAsync(_connectionString, Request.GetPersona().Scope, _impersonateUser, cancellationToken);
}
