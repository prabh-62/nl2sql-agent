using System.Data;
using Microsoft.Data.SqlClient;

namespace Nl2Sql.Data;

public sealed record QueryResult(
    string Sql,
    IReadOnlyList<string> Views,
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    bool IsTruncated,
    string? Error)
{
    public bool Succeeded => Error is null;

    public static QueryResult Failed(string sql, string error) => new(sql, [], [], [], false, error);
}

/// <summary>
/// Runs model-generated SQL: the guard first, then a compile check, then the query itself with a
/// timeout and a row cap, all on a <see cref="ReaderSession"/>. Errors come back as text for the
/// model to correct its query, never as exceptions.
/// </summary>
public static class QueryRunner
{
    public const int MaxRows = 500;

    public const int TimeoutSeconds = 15;

    public static async Task<QueryResult> RunAsync(ReaderSession session, string sql, CancellationToken cancellationToken)
    {
        var verdict = SqlGuard.Check(sql);
        if (!verdict.IsAllowed)
        {
            return QueryResult.Failed(sql, "Rejected by the SQL guard: " + string.Join(" ", verdict.Errors));
        }

        try
        {
            // Compile without running, so a broken query costs nothing and its error is precise.
            await using (var describe = new SqlCommand("EXEC sys.sp_describe_first_result_set @tsql = @sql;", session.Connection))
            {
                describe.Parameters.Add("@sql", SqlDbType.NVarChar, -1).Value = sql;
                describe.CommandTimeout = TimeoutSeconds;
                await using var _ = await describe.ExecuteReaderAsync(cancellationToken);
            }

            await using var command = new SqlCommand(sql, session.Connection) { CommandTimeout = TimeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i) is { Length: > 0 } name ? name : $"Column{i + 1}").ToArray();
            var rows = new List<object?[]>();
            var truncated = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count == MaxRows)
                {
                    // Stop reading; disposing the reader cancels the rest.
                    truncated = true;
                    command.Cancel();
                    break;
                }

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(values);
            }

            return new QueryResult(sql, verdict.Views, columns, rows, truncated, null);
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            return QueryResult.Failed(sql, $"The query took longer than {TimeoutSeconds} seconds and was stopped. Simplify it or filter to fewer rows.");
        }
        catch (SqlException exception)
        {
            return QueryResult.Failed(sql, "SQL Server error: " + exception.Message);
        }
    }
}
