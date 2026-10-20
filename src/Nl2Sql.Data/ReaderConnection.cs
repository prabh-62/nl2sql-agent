using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Nl2Sql.Data;

/// <summary>
/// Opens connections for running model-generated SQL. Every connection:
/// <list type="number">
/// <item>optionally switches to a reader user with <c>EXECUTE AS USER ... WITH COOKIE</c> (local development,
/// where the developer signs in as an Entra admin);</item>
/// <item>sets BrandId and LocationId in read-only session context for row-level security;</item>
/// <item>refuses to continue unless the effective user is in the <c>nl2sql_reader</c> role and not <c>db_owner</c>.</item>
/// </list>
/// Disposing reverts the switch with the cookie before the connection goes back to the pool.
/// Generated SQL can't revert without the cookie, and can't change the read-only session context.
/// </summary>
public static partial class ReaderConnection
{
    // The free-tier database auto-pauses when idle. The first connections after that fail with
    // "database is not currently available" (40613) for up to about a minute while it resumes.
    public static readonly SqlRetryLogicBaseProvider OpenRetry = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(
        new SqlRetryLogicOption
        {
            NumberOfTries = 8,
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(20),
        });

    public static async Task<ReaderSession> OpenAsync(
        string connectionString,
        TenantScope scope,
        string? impersonateUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var connection = new SqlConnection(connectionString) { RetryLogicProvider = OpenRetry };
        byte[]? cookie = null;
        try
        {
            await connection.OpenAsync(cancellationToken);

            if (!string.IsNullOrEmpty(impersonateUser))
            {
                if (!UserNamePattern().IsMatch(impersonateUser))
                {
                    throw new ArgumentException($"Invalid user name '{impersonateUser}'.", nameof(impersonateUser));
                }

                await using var impersonate = connection.CreateCommand();
                impersonate.CommandText = $"""
                    DECLARE @cookie varbinary(8000);
                    EXECUTE AS USER = '{impersonateUser}' WITH COOKIE INTO @cookie;
                    SELECT @cookie;
                    """;
                cookie = (byte[]?)await impersonate.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("EXECUTE AS USER returned no cookie.");
            }

            await using (var context = connection.CreateCommand())
            {
                context.CommandText = """
                    EXEC sys.sp_set_session_context @key = N'BrandId', @value = @BrandId, @read_only = 1;
                    EXEC sys.sp_set_session_context @key = N'LocationId', @value = @LocationId, @read_only = 1;
                    """;
                context.Parameters.AddWithValue("@BrandId", scope.BrandId);
                // 0 means the whole brand. Never NULL: a NULL parameter isn't locked by @read_only.
                context.Parameters.AddWithValue("@LocationId", scope.LocationId ?? 0);
                await context.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT CAST(ISNULL(IS_ROLEMEMBER('nl2sql_reader'), 0) AS int), CAST(ISNULL(IS_MEMBER('db_owner'), 0) AS int), USER_NAME();";
                await using var reader = await check.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                var isReader = reader.GetInt32(0) == 1;
                var isOwner = reader.GetInt32(1) == 1;
                if (!isReader || isOwner)
                {
                    throw new InvalidOperationException(
                        $"Refusing to run queries as database user '{reader.GetString(2)}': it must be a member of nl2sql_reader and not db_owner.");
                }
            }

            return new ReaderSession(connection, cookie);
        }
        catch
        {
            // Never return a connection to the pool while it may still be impersonating.
            SqlConnection.ClearPool(connection);
            await connection.DisposeAsync();
            throw;
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex UserNamePattern();
}

public sealed class ReaderSession : IAsyncDisposable
{
    private readonly byte[]? _cookie;

    internal ReaderSession(SqlConnection connection, byte[]? cookie)
    {
        Connection = connection;
        _cookie = cookie;
    }

    public SqlConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_cookie is not null && Connection.State == System.Data.ConnectionState.Open)
            {
                await using var revert = Connection.CreateCommand();
                revert.CommandText = "REVERT WITH COOKIE = @cookie;";
                revert.Parameters.Add("@cookie", System.Data.SqlDbType.VarBinary, 8000).Value = _cookie;
                await revert.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // If the revert failed, discard the pooled connection rather than reuse it.
            SqlConnection.ClearPool(Connection);
            throw;
        }
        finally
        {
            await Connection.DisposeAsync();
        }
    }
}
