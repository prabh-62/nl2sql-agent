using Microsoft.Data.SqlClient;
using Nl2Sql.Data;

namespace Nl2Sql.SafetyTests;

/// <summary>The agent's tools against the real database, as the reader.</summary>
public sealed class ToolTests
{
    [Fact]
    public async Task Catalog_lists_every_ai_view_with_a_description()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var views = await Catalog.ListViewsAsync(session, TestContext.Current.CancellationToken);

        Assert.Equal(["ai.Customers", "ai.Locations", "ai.MenuItems", "ai.OrderLineModifiers", "ai.OrderLines", "ai.Orders", "ai.Refunds"], views.Select(v => v.Name));
        Assert.All(views, v => Assert.False(string.IsNullOrWhiteSpace(v.Description), $"{v.Name} has no description."));
    }

    [Fact]
    public async Task Catalog_describes_every_column_of_a_view()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var columns = await Catalog.DescribeViewAsync(session, "ai.Orders", TestContext.Current.CancellationToken);

        Assert.NotNull(columns);
        Assert.All(columns, c => Assert.False(string.IsNullOrWhiteSpace(c.Description), $"ai.Orders.{c.Name} has no description."));
        Assert.Contains(columns, c => c.Name == "NetRevenue" && c.Type.StartsWith("decimal(", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("dbo.Orders")]
    [InlineData("Examples")]
    [InlineData("Orders'; DROP TABLE dbo.Orders; --")]
    public async Task Catalog_describes_nothing_outside_the_ai_views(string name)
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        Assert.Null(await Catalog.DescribeViewAsync(session, name, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Examples_are_ranked_by_the_question()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var examples = await Catalog.GetExamplesAsync(session, "What share of our lattes use oat milk?", 3, TestContext.Current.CancellationToken);

        Assert.Equal(3, examples.Count);
        Assert.Equal("Oat milk share of lattes, month over month", examples[0].Question);
    }

    [Theory]
    [InlineData(0)]   // owner
    [InlineData(101)] // manager
    public async Task Every_example_passes_the_guard_and_runs(int locationId)
    {
        await using var session = await SafetyDatabase.OpenAsync(new TenantScope(1, locationId == 0 ? null : locationId));
        var examples = await Catalog.GetExamplesAsync(session, "", 100, TestContext.Current.CancellationToken);

        Assert.NotEmpty(examples);
        foreach (var example in examples)
        {
            var result = await QueryRunner.RunAsync(session, example.Sql, TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded, $"'{example.Question}': {result.Error}");
            Assert.NotEmpty(result.Rows);
        }
    }

    [Fact]
    public async Task Run_query_respects_the_tenant_scope()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Manager(1, 101));

        var result = await QueryRunner.RunAsync(session, "SELECT DISTINCT LocationId FROM ai.Orders", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([101], result.Rows.Select(r => (int)r[0]!));
        Assert.Equal(["ai.Orders"], result.Views);
    }

    [Fact]
    public async Task Run_query_stops_reading_at_the_row_cap()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var result = await QueryRunner.RunAsync(session, "SELECT OrderId FROM ai.Orders", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(QueryRunner.MaxRows, result.Rows.Count);
        Assert.True(result.IsTruncated);

        // The connection is still usable afterwards.
        Assert.Equal(1, await session.Connection.ScalarAsync<int>("SELECT 1;"));
    }

    [Fact]
    public async Task Run_query_returns_sql_errors_for_the_model_to_fix()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var result = await QueryRunner.RunAsync(session, "SELECT Revenue FROM ai.Orders", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid column name 'Revenue'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_query_never_sends_rejected_sql_to_the_database()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        var result = await QueryRunner.RunAsync(session, "SELECT * FROM dbo.Orders", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Rejected by the SQL guard", result.Error, StringComparison.Ordinal);
    }
}
