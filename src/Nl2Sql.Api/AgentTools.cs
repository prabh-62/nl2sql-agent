using System.ComponentModel;
using System.Text.Json;
using Nl2Sql.Data;

namespace Nl2Sql.Api;

/// <summary>
/// The tools the agent can call. Each opens a reader connection scoped to the current tenant, so the
/// model only ever sees what that tenant's reader can see.
/// </summary>
public sealed class AgentTools(IConfiguration configuration)
{
    // The model sees at most this many rows; the full result (up to QueryRunner.MaxRows) is for the UI.
    private const int RowsForModel = 50;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _connectionString = configuration.GetConnectionString("db")
        ?? throw new InvalidOperationException("Connection string 'db' is missing.");

    private readonly string? _impersonateUser = configuration["Database:ImpersonateUser"];

    // Until personas arrive in M3, every request runs as the Maple & Bean owner.
    private readonly TenantScope _scope = new(
        configuration.GetValue("Demo:BrandId", 1),
        configuration.GetValue<int?>("Demo:LocationId"));

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

    [Description("Runs one T-SQL SELECT against the ai views and returns the result. If it fails, read the error, fix the query and try again (at most twice).")]
    public async Task<string> RunQueryAsync(
        [Description("A single T-SQL SELECT statement that reads only views in schema ai.")] string sql,
        CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(cancellationToken);
        var result = await QueryRunner.RunAsync(session, sql, cancellationToken);
        if (!result.Succeeded)
        {
            return JsonSerializer.Serialize(new { error = result.Error }, Json);
        }

        return JsonSerializer.Serialize(new
        {
            columns = result.Columns,
            rows = result.Rows.Take(RowsForModel),
            rowCount = result.Rows.Count,
            truncated = result.IsTruncated || result.Rows.Count > RowsForModel,
            views = result.Views,
        }, Json);
    }

    private Task<ReaderSession> OpenAsync(CancellationToken cancellationToken) =>
        ReaderConnection.OpenAsync(_connectionString, _scope, _impersonateUser, cancellationToken);
}
