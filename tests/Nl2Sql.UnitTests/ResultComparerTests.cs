using Nl2Sql.Evals;

namespace Nl2Sql.UnitTests;

public sealed class ResultComparerTests
{
    private static readonly object?[][] Reference =
    [
        ["Queen St W", 5070.74m],
        ["Union Station", 5297.07m],
    ];

    [Fact]
    public void Ignores_row_order_column_order_names_and_extra_columns()
    {
        object?[][] agent =
        [
            [5297.07, "union station", 1200],
            [5070.74, "Queen St W", 1100],
        ];

        Assert.True(ResultComparer.Matches(Reference, agent));
    }

    [Fact]
    public void Allows_small_rounding_differences()
    {
        object?[][] agent = [["Queen St W", 5070.7], ["Union Station", 5297.1]];

        Assert.True(ResultComparer.Matches(Reference, agent));
    }

    [Fact]
    public void Rejects_wrong_numbers()
    {
        object?[][] agent = [["Queen St W", 5070.74], ["Union Station", 5400.00]];

        Assert.False(ResultComparer.Matches(Reference, agent));
    }

    [Fact]
    public void Rejects_values_that_only_match_column_by_column_but_not_row_by_row()
    {
        object?[][] agent = [["Queen St W", 5297.07], ["Union Station", 5070.74]];

        Assert.False(ResultComparer.Matches(Reference, agent));
    }

    [Fact]
    public void Rejects_missing_or_extra_rows()
    {
        Assert.False(ResultComparer.Matches(Reference, [["Queen St W", 5070.74]]));
        Assert.False(ResultComparer.Matches(Reference, [.. Reference, ["Square One", 1.0]]));
    }

    [Fact]
    public void Accepts_a_share_as_a_fraction_or_a_percentage()
    {
        object?[][] reference = [["mobile", 0.0899]];

        Assert.True(ResultComparer.Matches(reference, [["mobile", 8.99]]));
        Assert.True(ResultComparer.Matches(reference, [["mobile", 0.0899m]]));
    }

    [Fact]
    public void Checks_order_when_it_matters_and_allows_a_longer_ranked_list()
    {
        object?[][] reference = [["Caffè Latte", 493.1m], ["Iced Caffè Latte", 400m]];
        object?[][] longer = [["Caffè Latte", 493.1], ["Iced Caffè Latte", 400.0], ["Flat White", 300.0]];
        object?[][] swapped = [["Iced Caffè Latte", 400.0], ["Caffè Latte", 493.1]];

        Assert.True(ResultComparer.Matches(reference, longer, ordered: true, prefix: true));
        Assert.False(ResultComparer.Matches(reference, swapped, ordered: true));
    }

    [Fact]
    public void Treats_an_aggregate_over_nothing_as_no_rows()
    {
        object?[][] none = [[null]];

        Assert.True(ResultComparer.Matches([], none));
        Assert.False(ResultComparer.Matches(Reference, none));
    }

    [Fact]
    public void Compares_dates_by_day()
    {
        object?[][] reference = [[new DateTime(2026, 9, 1), 0.35m]];

        Assert.True(ResultComparer.Matches(reference, [[new DateTime(2026, 9, 1), 0.35]]));
        Assert.False(ResultComparer.Matches(reference, [[new DateTime(2026, 8, 1), 0.35]]));
    }
}
