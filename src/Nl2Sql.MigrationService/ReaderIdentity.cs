using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Nl2Sql.MigrationService;

/// <summary>
/// Adds the nl2sql-reader managed identity (used by App Service and CI) as a database user in the
/// nl2sql_reader role. The identity differs per environment, so it can't live in the dacpac.
/// Created from its client ID (WITH SID ..., TYPE = E), so the server needs no Microsoft Graph access.
/// </summary>
internal static partial class ReaderIdentity
{
    public static async Task EnsureAsync(string connectionString, string? name, string? clientId, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(clientId))
        {
            logger.NoReaderIdentity();
            return;
        }

        if (!NamePattern().IsMatch(name) || !Guid.TryParse(clientId, out var id))
        {
            throw new InvalidOperationException("ReaderIdentity:Name or ReaderIdentity:ClientId is invalid.");
        }

        // Names can't be parameters in DDL; both values are validated above.
        var sql = $"""
            IF DATABASE_PRINCIPAL_ID(N'{name}') IS NULL
                CREATE USER [{name}] WITH SID = {Sid(id)}, TYPE = E;
            IF ISNULL(IS_ROLEMEMBER(N'nl2sql_reader', N'{name}'), 0) = 0
                ALTER ROLE nl2sql_reader ADD MEMBER [{name}];
            GRANT CONNECT TO [{name}];
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.ReaderIdentityReady(name);
    }

    private static string Sid(Guid clientId) => "0x" + Convert.ToHexString(clientId.ToByteArray());

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex NamePattern();
}
