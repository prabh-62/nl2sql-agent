using Microsoft.Data.SqlClient;
using Nl2Sql.Data;

namespace Nl2Sql.SafetyTests;

public sealed class ReadOnlyTests
{
    public static TheoryData<string> ForbiddenStatements =>
    [
        // Writes, through the views and directly.
        "INSERT INTO dbo.Brands (BrandId, Name) VALUES (99, N'Evil Corp');",
        "UPDATE dbo.Orders SET Status = 'voided';",
        "DELETE FROM dbo.Refunds;",
        "TRUNCATE TABLE dbo.Refunds;",
        "UPDATE ai.Orders SET Channel = 'mobile';",
        "DELETE FROM ai.Refunds;",
        // Reading anything but the ai views.
        "SELECT TOP (1) OrderId FROM dbo.Orders;",
        "SELECT TOP (1) SeedVersion FROM dbo.SeedHistory;",
        // Schema and security changes.
        "CREATE TABLE ai.Evil (Id int);",
        "DROP VIEW ai.Orders;",
        "ALTER SECURITY POLICY security.TenantPolicy WITH (STATE = OFF);",
        "GRANT SELECT ON SCHEMA::dbo TO nl2sql_reader;",
        // Escaping the tenant scope or the reader identity.
        "EXEC sys.sp_set_session_context @key = N'BrandId', @value = 2;",
        "EXEC sys.sp_set_session_context @key = N'LocationId', @value = NULL;",
        "EXEC sys.sp_set_session_context @key = N'LocationId', @value = 101;",
        "REVERT;",
        "EXECUTE AS USER = 'dbo';",
    ];

    [Theory]
    [MemberData(nameof(ForbiddenStatements))]
    public async Task Reader_cannot(string sql)
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        await Assert.ThrowsAsync<SqlException>(() => session.Connection.ExecuteAsync(sql));

        // The connection is still the scoped reader afterwards.
        Assert.Equal(1, await session.Connection.ScalarAsync<int>("SELECT CAST(SESSION_CONTEXT(N'BrandId') AS int);"));
        Assert.Equal(1, await session.Connection.ScalarAsync<int>("SELECT IS_ROLEMEMBER('nl2sql_reader');"));
    }

    [Fact]
    public async Task Reader_can_read_the_ai_views()
    {
        await using var session = await SafetyDatabase.OpenAsync(TenantScope.Owner(1));

        foreach (var view in new[] { "Locations", "MenuItems", "Orders", "OrderLines", "OrderLineModifiers", "Customers", "Refunds" })
        {
            Assert.True(await session.Connection.ScalarAsync<int>($"SELECT COUNT(*) FROM ai.{view};") > 0, $"ai.{view} returned no rows.");
        }
    }
}
