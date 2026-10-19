namespace Nl2Sql.MigrationService;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed.")]
    public static partial void MigrationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "DacFx: {Message}")]
    public static partial void DacFxMessage(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "No reader identity configured; skipping.")]
    public static partial void NoReaderIdentity(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reader identity {Name} is a member of nl2sql_reader.")]
    public static partial void ReaderIdentityReady(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed version {Version} is already loaded.")]
    public static partial void SeedAlreadyLoaded(this ILogger logger, int version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading seed version {Version}: {Orders} orders from {First} to {Last}.")]
    public static partial void LoadingSeed(this ILogger logger, int version, int orders, DateOnly first, DateOnly last);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Rows} rows into dbo.{Table}.")]
    public static partial void LoadedTable(this ILogger logger, int rows, string table);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed version {Version} loaded.")]
    public static partial void SeedLoaded(this ILogger logger, int version);
}
