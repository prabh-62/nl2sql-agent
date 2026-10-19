using Microsoft.Data.SqlClient;

namespace Nl2Sql.MigrationService;

/// <summary>
/// Loads <see cref="SeedData"/> once. Bump <see cref="SeedVersion"/> when the generator changes;
/// the next run then replaces the data.
/// </summary>
internal static class Seeder
{
    public const int SeedVersion = 1;

    private static readonly string[] TablesInDeleteOrder =
        ["Refunds", "Payments", "OrderLineModifiers", "OrderLines", "Orders", "Customers", "MenuItems", "Modifiers", "Sizes", "Locations", "Brands"];

    public static async Task EnsureSeededAsync(string connectionString, ILogger logger, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // SeedHistory isn't behind row-level security, unlike the tenant tables (which show an admin
        // without session context no rows at all).
        await using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.SeedHistory WHERE SeedVersion = @version;", connection))
        {
            check.Parameters.AddWithValue("@version", SeedVersion);
            if ((int)(await check.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                logger.SeedAlreadyLoaded(SeedVersion);
                return;
            }
        }

        var anchorDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var data = SeedData.Generate(anchorDate);
        var lastDay = anchorDate.AddDays(-1);
        logger.LoadingSeed(SeedVersion, data.OrderCount, data.FirstDay, lastDay);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // Row-level security filters DELETE too, so switch the policy off for the reload. DDL is
        // transactional: if anything fails, the policy is back on after the rollback.
        var clear = "ALTER SECURITY POLICY security.TenantPolicy WITH (STATE = OFF);\n"
            + string.Concat(TablesInDeleteOrder.Select(t => $"DELETE FROM dbo.{t};\n"))
            + "DELETE FROM dbo.SeedHistory;\n"
            + "ALTER SECURITY POLICY security.TenantPolicy WITH (STATE = ON);";
        await using (var command = new SqlCommand(clear, connection, transaction) { CommandTimeout = 300 })
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var table in data.AllTables)
        {
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction)
            {
                DestinationTableName = $"dbo.{table.TableName}",
                BatchSize = 10_000,
                BulkCopyTimeout = 600,
            };
            foreach (System.Data.DataColumn column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(table, cancellationToken);
            logger.LoadedTable(table.Rows.Count, table.TableName);
        }

        await using (var history = new SqlCommand(
            "INSERT dbo.SeedHistory (SeedVersion, AnchorDate, OrderCount, SeededAtUtc) VALUES (@version, @anchor, @orders, SYSUTCDATETIME());",
            connection, transaction))
        {
            history.Parameters.AddWithValue("@version", SeedVersion);
            history.Parameters.AddWithValue("@anchor", anchorDate.ToDateTime(TimeOnly.MinValue));
            history.Parameters.AddWithValue("@orders", data.OrderCount);
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.SeedLoaded(SeedVersion);
    }
}
