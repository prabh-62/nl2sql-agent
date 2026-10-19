using System.Threading.RateLimiting;

namespace Nl2Sql.Api;

public sealed class QuotaOptions
{
    public int QuestionsPerClientPerDay { get; set; } = 10;

    public int QuestionsPerDay { get; set; } = 200;
}

public enum QuotaVerdict
{
    Allowed,
    ClientLimitReached,
    DemoPaused,
}

/// <summary>
/// The public demo's limits, built on System.Threading.RateLimiting: a fixed one-day window per
/// client IP, and one for everybody. Counters live in memory, so they reset when the app restarts
/// (fine for one App Service instance).
/// </summary>
public sealed class QuestionQuota : IDisposable
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly PartitionedRateLimiter<string> _perClient;
    private readonly FixedWindowRateLimiter _global;

    public QuestionQuota(QuotaOptions options)
    {
        Options = options;
        _perClient = PartitionedRateLimiter.Create<string, string>(client =>
            RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.QuestionsPerClientPerDay,
                Window = Day,
                QueueLimit = 0,
            }));
        _global = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.QuestionsPerDay,
            Window = Day,
            QueueLimit = 0,
        });
    }

    public QuotaOptions Options { get; }

    public QuotaVerdict TryConsume(string client)
    {
        // Check the shared cap first so a paused demo doesn't use up anyone's own questions.
        if (_global.GetStatistics()?.CurrentAvailablePermits <= 0)
        {
            return QuotaVerdict.DemoPaused;
        }

        using var clientLease = _perClient.AttemptAcquire(client);
        if (!clientLease.IsAcquired)
        {
            return QuotaVerdict.ClientLimitReached;
        }

        using var globalLease = _global.AttemptAcquire();
        return globalLease.IsAcquired ? QuotaVerdict.Allowed : QuotaVerdict.DemoPaused;
    }

    public (long Remaining, int Limit, bool DemoPaused) Remaining(string client)
    {
        var remaining = _perClient.GetStatistics(client)?.CurrentAvailablePermits ?? Options.QuestionsPerClientPerDay;
        var paused = _global.GetStatistics()?.CurrentAvailablePermits <= 0;
        return (remaining, Options.QuestionsPerClientPerDay, paused);
    }

    public void Dispose()
    {
        _perClient.Dispose();
        _global.Dispose();
    }
}
