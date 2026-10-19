using Nl2Sql.Data;

namespace Nl2Sql.SafetyTests;

public sealed class ReaderConnectionTests
{
    [Fact]
    public async Task Pooled_connections_do_not_leak_scope_between_tenants()
    {
        await using (var first = await SafetyDatabase.OpenAsync(TenantScope.Owner(1)))
        {
            Assert.Equal(1, await first.Connection.ScalarAsync<int>("SELECT CAST(SESSION_CONTEXT(N'BrandId') AS int);"));
        }

        await using var second = await SafetyDatabase.OpenAsync(TenantScope.Owner(2));
        var locations = await second.Connection.ColumnAsync<int>("SELECT DISTINCT LocationId FROM ai.Orders ORDER BY LocationId;");

        Assert.Equal(2, await second.Connection.ScalarAsync<int>("SELECT CAST(SESSION_CONTEXT(N'BrandId') AS int);"));
        Assert.Equal(SafetyDatabase.LocationsByBrand[2], locations);
    }

    [Fact]
    public async Task Refuses_to_run_as_a_privileged_user()
    {
        Assert.SkipUnless(SafetyDatabase.Impersonates, "Only meaningful when signed in as an admin (local runs).");

        // Signed in as an Entra admin without switching to the reader: must refuse.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReaderConnection.OpenAsync(SafetyDatabase.ConnectionString, TenantScope.Owner(1), impersonateUser: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_unsafe_impersonation_user_names()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderConnection.OpenAsync(SafetyDatabase.ConnectionString, TenantScope.Owner(1), "dbo'; DROP TABLE dbo.Orders; --", TestContext.Current.CancellationToken));
    }
}
