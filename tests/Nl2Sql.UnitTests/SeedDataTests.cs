using System.Data;
using Nl2Sql.MigrationService;

namespace Nl2Sql.UnitTests;

public sealed class SeedDataTests
{
    private static readonly DateOnly Anchor = new(2026, 10, 19);
    private static readonly SeedData Data = SeedData.Generate(Anchor);

    [Fact]
    public void Same_anchor_date_gives_identical_data()
    {
        var again = SeedData.Generate(Anchor);

        Assert.Equal(Fingerprint(Data), Fingerprint(again));
    }

    [Fact]
    public void Covers_twelve_months_ending_the_day_before_the_anchor()
    {
        var placed = Data.Orders.AsEnumerable().Select(r => r.Field<DateTime>("PlacedAtUtc")).ToArray();

        Assert.InRange(placed.Min(), Anchor.AddDays(-366).ToDateTime(TimeOnly.MinValue), Anchor.AddDays(-364).ToDateTime(TimeOnly.MinValue));
        Assert.True(placed.Max() < Anchor.AddDays(1).ToDateTime(TimeOnly.MinValue));
        Assert.InRange(Data.OrderCount, 50_000, 100_000);
    }

    // Row-level security filters each table on its own BrandId/LocationId columns, so those copies
    // must always match the order they belong to.
    [Theory]
    [InlineData("OrderLines")]
    [InlineData("Payments")]
    [InlineData("Refunds")]
    public void Tenant_keys_match_the_order(string tableName)
    {
        var orders = Data.Orders.AsEnumerable().ToDictionary(r => r.Field<long>("OrderId"), r => (r.Field<int>("BrandId"), r.Field<int>("LocationId")));
        var table = Data.AllTables.Single(t => t.TableName == tableName);

        Assert.All(table.AsEnumerable(), row =>
            Assert.Equal(orders[row.Field<long>("OrderId")], (row.Field<int>("BrandId"), row.Field<int>("LocationId"))));
    }

    [Fact]
    public void Modifier_tenant_keys_match_the_order_line()
    {
        var lines = Data.OrderLines.AsEnumerable().ToDictionary(r => r.Field<long>("OrderLineId"), r => (r.Field<int>("BrandId"), r.Field<int>("LocationId")));

        Assert.All(Data.OrderLineModifiers.AsEnumerable(), row =>
            Assert.Equal(lines[row.Field<long>("OrderLineId")], (row.Field<int>("BrandId"), row.Field<int>("LocationId"))));
    }

    [Fact]
    public void Orders_reference_customers_and_menu_items_of_the_same_brand()
    {
        var customerBrands = Data.Customers.AsEnumerable().ToDictionary(r => r.Field<long>("CustomerId"), r => r.Field<int>("BrandId"));
        var menuBrands = Data.MenuItems.AsEnumerable().ToDictionary(r => r.Field<int>("MenuItemId"), r => r.Field<int>("BrandId"));

        Assert.All(Data.Orders.AsEnumerable().Where(r => !r.IsNull("CustomerId")), row =>
            Assert.Equal(row.Field<int>("BrandId"), customerBrands[row.Field<long>("CustomerId")]));
        Assert.All(Data.OrderLines.AsEnumerable(), row =>
            Assert.Equal(row.Field<int>("BrandId"), menuBrands[row.Field<int>("MenuItemId")]));
    }

    [Fact]
    public void Oat_milk_share_grows_over_the_year()
    {
        var firstDay = Anchor.AddDays(-365).ToDateTime(TimeOnly.MinValue);
        var placed = Data.Orders.AsEnumerable().ToDictionary(r => r.Field<long>("OrderId"), r => r.Field<DateTime>("PlacedAtUtc"));
        var lineOrder = Data.OrderLines.AsEnumerable().ToDictionary(r => r.Field<long>("OrderLineId"), r => r.Field<long>("OrderId"));
        var milk = Data.OrderLineModifiers.AsEnumerable()
            .Where(r => r.Field<int>("ModifierId") <= 5)
            .Select(r => (Quarter: (placed[lineOrder[r.Field<long>("OrderLineId")]] - firstDay).Days / 92, IsOat: r.Field<int>("ModifierId") == 3))
            .ToArray();

        double Share(int quarter) => milk.Where(m => m.Quarter == quarter).Average(m => m.IsOat ? 1.0 : 0.0);

        Assert.True(Share(3) > Share(0) + 0.08, $"Oat share went from {Share(0):P0} to {Share(3):P0}.");
    }

    private static string Fingerprint(SeedData data) =>
        string.Join("|", data.AllTables.Select(t => $"{t.TableName}:{t.Rows.Count}:{string.Join(",", t.AsEnumerable().Take(50).Select(r => string.Join(";", r.ItemArray)))}"));
}
