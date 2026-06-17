using System.Collections.Concurrent;
using ApiRateLimiter.Core.Interfaces;
using ApiRateLimiter.Core.Models;
using Microsoft.Extensions.Options;

namespace ApiRateLimiter.Core.Services;

// In-memory fixed window: each client gets Limit requests per Window.
// Thread safety is achieved without locks using Interlocked and ConcurrentDictionary.
public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, WindowBucket> _buckets = new();

    public FixedWindowRateLimiter(IOptions<RateLimitOptions> options)
    {
        _options = options.Value;
    }

    public Task<RateLimitResult> IsAllowedAsync(string clientId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var bucket = _buckets.GetOrAdd(clientId, _ => new WindowBucket(now, _options.Window));

        // If the current window has expired, swap in a fresh bucket atomically.
        if (now >= bucket.ExpiresAt)
        {
            var fresh = new WindowBucket(now, _options.Window);
            // Only the thread that wins the swap counts its request in the new bucket.
            bucket = _buckets.AddOrUpdate(clientId, fresh, (_, existing) =>
                now >= existing.ExpiresAt ? fresh : existing);
        }

        var count = Interlocked.Increment(ref bucket.RequestCount);
        var allowed = count <= _options.Limit;

        return Task.FromResult(new RateLimitResult(
            IsAllowed: allowed,
            Remaining: Math.Max(0, _options.Limit - (int)count),
            Limit: _options.Limit,
            ResetsAt: bucket.ExpiresAt
        ));
    }

    private sealed class WindowBucket(DateTimeOffset start, TimeSpan window)
    {
        public readonly DateTimeOffset ExpiresAt = start + window;
        public int RequestCount;  // mutated only via Interlocked
    }
}
