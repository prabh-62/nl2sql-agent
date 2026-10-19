using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace Nl2Sql.MigrationService;

/// <summary>
/// Brings the database up to date, then exits: deploys the dacpac, adds the reader identity to the
/// nl2sql_reader role when one is configured, and loads the deterministic seed if it isn't loaded yet.
/// </summary>
public sealed class Worker(
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "Nl2Sql.MigrationService";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Migrate database", ActivityKind.Client);
        try
        {
            var connectionString = configuration.GetConnectionString("db")
                ?? throw new InvalidOperationException("Connection string 'db' is missing.");

            DeploySchema(connectionString);
            await ReaderIdentity.EnsureAsync(connectionString, configuration["ReaderIdentity:Name"], configuration["ReaderIdentity:ClientId"], logger, stoppingToken);
            await Seeder.EnsureSeededAsync(connectionString, logger, stoppingToken);
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            logger.MigrationFailed(exception);
            Environment.ExitCode = 1;
        }

        lifetime.StopApplication();
    }

    private void DeploySchema(string connectionString)
    {
        var dacpacPath = Path.Combine(AppContext.BaseDirectory, "Nl2Sql.Database.dacpac");
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var services = new DacServices(connectionString);
        services.Message += (_, e) => logger.DacFxMessage(e.Message.Message);

        using var package = DacPackage.Load(dacpacPath);
        services.Deploy(package, databaseName, upgradeExisting: true, new DacDeployOptions
        {
            // Never lose data, and never touch objects this project doesn't own
            // (the database also holds unrelated tables, and the reader identity's user).
            BlockOnPossibleDataLoss = true,
            DropObjectsNotInSource = false,
            DropPermissionsNotInSource = false,
            DropRoleMembersNotInSource = false,
            // Changing a free-tier database's settings is not this tool's job.
            ScriptDatabaseOptions = false,
            // One transaction: when the security policy has to be dropped and recreated, no other
            // session ever sees the tables without row-level security.
            IncludeTransactionalScripts = true,
        });
    }
}
