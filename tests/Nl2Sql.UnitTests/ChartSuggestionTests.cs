using Nl2Sql.Agent;

namespace Nl2Sql.UnitTests;

public sealed class ChartSuggestionTests
{
    private static readonly string[] Columns = ["Month", "NetRevenue", "Orders", "LocationName"];

    private static readonly object?[][] Rows =
    [
        [new DateTime(2026, 8, 1), 5100.25m, 1200, "Queen St W"],
        [new DateTime(2026, 9, 1), 5300.75m, 1250, "Queen St W"],
    ];

    [Fact]
    public void Accepts_numeric_columns_that_exist() =>
        Assert.Null(new ChartSuggestion("line", "Revenue", "Month", ["NetRevenue", "Orders"]).Validate(Columns, Rows));

    [Fact]
    public void Matches_column_names_case_insensitively() =>
        Assert.Null(new ChartSuggestion("bar", "Revenue", "month", ["netrevenue"]).Validate(Columns, Rows));

    [Theory]
    [InlineData("pie", "Month", "NetRevenue", "bar or line")]
    [InlineData("bar", "Week", "NetRevenue", "'Week' is not a column")]
    [InlineData("bar", "Month", "Revenue", "'Revenue' is not a column")]
    [InlineData("bar", "Month", "LocationName", "not numeric")]
    public void Rejects_charts_that_do_not_fit_the_result(string type, string x, string y, string expected) =>
        Assert.Contains(expected, new ChartSuggestion(type, "t", x, [y]).Validate(Columns, Rows), StringComparison.Ordinal);

    [Fact]
    public void Needs_one_to_three_series()
    {
        Assert.NotNull(new ChartSuggestion("bar", "t", "Month", []).Validate(Columns, Rows));
        Assert.NotNull(new ChartSuggestion("bar", "t", "Month", ["NetRevenue", "Orders", "NetRevenue", "Orders"]).Validate(Columns, Rows));
    }

    [Fact]
    public void Needs_at_least_two_rows() =>
        Assert.NotNull(new ChartSuggestion("bar", "t", "Month", ["Orders"]).Validate(Columns, Rows[..1]));
}
