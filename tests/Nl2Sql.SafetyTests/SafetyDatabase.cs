using Microsoft.Data.SqlClient;
using Nl2Sql.Data;

namespace Nl2Sql.SafetyTests;

/// <summary>
/// The real database the safety tests run against.
/// <list type="bullet">
/// <item><b>Locally</b> (default): you sign in as an Entra admin, and every query switches to
/// <c>nl2sql_reader_dev</c>, exactly like the API does in local development.</item>
/// <item><b>In CI</b> (<c>NL2SQL_SAFETY_MODE=direct</c>): the job signs in as the nl2sql-reader managed
/// identity with GitHub OIDC, the identity App Service uses in production.</item>
/// </list>
/// </summary>
internal static class SafetyDatabase
{
    public const string DevReaderUser = "nl2sql_reader_dev";

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("NL2SQL_SAFETY_CONNECTION")
        ?? "Server=tcp:panjab.database.windows.net,1433;Database=rasoolpur;Authentication=Active Directory Default;Encrypt=True;Connect Timeout=90";

    public static readonly bool Impersonates = Environment.GetEnvironmentVariable("NL2SQL_SAFETY_MODE") != "direct";

    public static readonly IReadOnlyDictionary<int, int[]> LocationsByBrand = new Dictionary<int, int[]>
    {
        [1] = [101, 102, 103, 104],
        [2] = [201, 202, 203],
        [3] = [301, 302],
    };

    static SafetyDatabase()
    {
        // Only try developer credentials (Azure CLI, including the one azure/login sets up in CI),
        // instead of first probing for a managed identity.
        if (Environment.GetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS") is null)
        {
            Environment.SetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS", "dev");
        }
    }

    public static Task<ReaderSession> OpenAsync(TenantScope scope) =>
        ReaderConnection.OpenAsync(ConnectionString, scope, Impersonates ? DevReaderUser : null, TestContext.Current.CancellationToken);

    /// <summary>A reader connection with no session context at all, bypassing <see cref="ReaderConnection"/>.</summary>
    public static async Task<SqlConnection> OpenWithoutContextAsync()
    {
        var connection = new SqlConnection(ConnectionString + ";Pooling=false");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        if (Impersonates)
        {
            await connection.ExecuteAsync($"EXECUTE AS USER = '{DevReaderUser}';");
        }

        return connection;
    }
}

internal static class SqlConnectionExtensions
{
    public static async Task ExecuteAsync(this SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(this SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is DBNull or null ? default! : (T)value;
    }

    public static async Task<List<T>> ColumnAsync<T>(this SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var values = new List<T>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetFieldValue<T>(0));
        }

        return values;
    }
}
