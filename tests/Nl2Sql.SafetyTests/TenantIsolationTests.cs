using Nl2Sql.Data;

namespace Nl2Sql.SafetyTests;

public sealed class TenantIsolationTests
{
    public static TheoryData<int> Brands => [1, 2, 3];

    public static TheoryData<string, string> LocationColumns => new()
    {
        { "ai.Locations", "LocationId" },
        { "ai.Orders", "LocationId" },
        { "ai.OrderLines", "LocationId" },
        { "ai.OrderLineModifiers", "LocationId" },
        { "ai.Refunds", "LocationId" },
        { "ai.Customers", "HomeLocationId" },
    };

    [Theory]
    [MemberData(nameof(Brands))]
    public async Task Owner_sees_every_location_of_their_brand_and_nothing_else(int brandId)
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(brandId));

        var locations = await session.Connection.ColumnAsync<int>("SELECT DISTINCT LocationId FROM ai.Orders ORDER BY LocationId;");

        Assert.Equal(SafetyDatabase.LocationsByBrand[brandId], locations);
    }

    [Theory]
    [MemberData(nameof(Brands))]
    public async Task Owner_sees_only_their_brands_menu(int brandId)
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(brandId));

        var menuItems = await session.Connection.ColumnAsync<int>("SELECT MenuItemId FROM ai.MenuItems;");

        Assert.NotEmpty(menuItems);
        Assert.All(menuItems, id => Assert.Equal(brandId, id / 100));
    }

    [Theory]
    [MemberData(nameof(LocationColumns))]
    public async Task Manager_sees_only_their_location_in_every_view(string view, string column)
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Manager(1, 101));

        var locations = await session.Connection.ColumnAsync<int>($"SELECT DISTINCT {column} FROM {view};");

        Assert.Equal([101], locations);
    }

    [Fact]
    public async Task Manager_asking_about_another_location_gets_nothing()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Manager(1, 101));

        var orders = await session.Connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Orders WHERE LocationId = 102;");
        var revenue = await session.Connection.ScalarAsync<decimal?>("SELECT SUM(NetRevenue) FROM ai.Orders WHERE LocationName = N'Union Station';");

        Assert.Equal(0, orders);
        Assert.Null(revenue);
    }

    [Fact]
    public async Task Owner_asking_about_another_brand_gets_nothing()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(2));

        var orders = await session.Connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Orders WHERE LocationId IN (101, 102, 103, 104, 301, 302);");
        var brands = await session.Connection.ColumnAsync<string>("SELECT DISTINCT BrandName FROM ai.Locations;");

        Assert.Equal(0, orders);
        Assert.Equal(["Starbucks"], brands);
    }

    [Fact]
    public async Task Manager_scope_cannot_name_a_location_of_another_brand()
    {
        // A location ID from another brand matches nothing: the brand check comes first.
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Manager(1, 201));

        var orders = await session.Connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Orders;");

        Assert.Equal(0, orders);
    }

    [Fact]
    public async Task Manager_cannot_widen_their_scope_to_the_whole_brand()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Manager(1, 101));

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            session.Connection.ExecuteAsync("EXEC sys.sp_set_session_context @key = N'LocationId', @value = 0;"));
        Assert.Equal([101], await session.Connection.ColumnAsync<int>("SELECT DISTINCT LocationId FROM ai.Orders;"));
    }

    [Fact]
    public async Task Brand_without_location_key_fails_closed()
    {
        await using var connection = await SafetyDatabase.OpenWithoutContextAsync();
        await connection.ExecuteAsync("EXEC sys.sp_set_session_context @key = N'BrandId', @value = 1, @read_only = 1;");

        Assert.Equal(0, await connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Orders;"));
    }

    [Fact]
    public async Task No_session_context_means_no_rows()
    {
        await using var connection = await SafetyDatabase.OpenWithoutContextAsync();

        var orders = await connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Orders;");
        var locations = await connection.ScalarAsync<int>("SELECT COUNT(*) FROM ai.Locations;");

        Assert.Equal(0, orders);
        Assert.Equal(0, locations);
    }
}
