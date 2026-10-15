using System.Diagnostics;
using System.Threading.RateLimiting;

namespace Library.Web.Api;

internal sealed class ApiWindowLimiter(int permitLimit) : RateLimiter
{
    private readonly object gate = new();
    private readonly FixedWindowRateLimiter limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        AutoReplenishment = false,
        QueueLimit = 0
    });
    private long windowStart = Stopwatch.GetTimestamp();

    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (gate)
            {
                ReplenishIfNeeded();
                return limiter.IdleDuration;
            }
        }
    }
    public override RateLimiterStatistics? GetStatistics() => limiter.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        lock (gate)
        {
            ReplenishIfNeeded();
            var lease = limiter.AttemptAcquire(permitCount);
            if (lease.IsAcquired)
                return lease;
            // Report the remaining time rather than another full minute.
            var remaining = TimeSpan.FromMinutes(1) - Stopwatch.GetElapsedTime(windowStart);
            return new RejectedLease(lease, remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1));
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        => ValueTask.FromResult(AttemptAcquireCore(permitCount));

    private void ReplenishIfNeeded()
    {
        if (Stopwatch.GetElapsedTime(windowStart) >= TimeSpan.FromMinutes(1) && limiter.TryReplenish())
            windowStart = Stopwatch.GetTimestamp();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            limiter.Dispose();
        base.Dispose(disposing);
    }

    private sealed class RejectedLease(RateLimitLease lease, TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
            return metadata is not null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                lease.Dispose();
            base.Dispose(disposing);
        }
    }
}
