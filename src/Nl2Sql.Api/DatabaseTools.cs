using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace Nl2Sql.Api;

public sealed class DatabaseTools(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("db")
        ?? throw new InvalidOperationException("Connection string 'db' is missing.");

    // M0 connectivity check only. It runs as the signed-in developer (Entra admin); the read-only
    // principal, row-level security and the SQL guard arrive in M1 and M2.
    [Description("Checks the database connection and returns the SQL Server version.")]
    public async Task<string> PingDbAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("SELECT @@VERSION", connection) { CommandTimeout = 30 };
        return (string)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
