using System.Collections.Concurrent;
using ApiRateLimiter.Core.Models;

namespace ApiRateLimiter.Core.Services;

// In-memory sliding window: tracks per-request timestamps so the window rolls
// forward continuously rather than resetting on a fixed boundary.
// Each client entry holds a timestamp queue and a dedicated lock for safe mutation.
public sealed class SlidingWindowRateLimiter
{
    private readonly ConcurrentDictionary<string, (Queue<long> Timestamps, object Lock)> _requestLogs = new();

    public Task<RateLimitResult> IsAllowedAsync(string clientId, int windowSeconds, int maxRequests)
    {
        var entry = _requestLogs.GetOrAdd(clientId, _ => (new Queue<long>(), new object()));
        var now = DateTimeOffset.UtcNow;
        var windowStartMs = now.AddSeconds(-windowSeconds).ToUnixTimeMilliseconds();

        lock (entry.Lock)
        {
            // Evict timestamps outside the rolling window.
            while (entry.Timestamps.Count > 0 && entry.Timestamps.Peek() <= windowStartMs)
                entry.Timestamps.Dequeue();

            var allowed = entry.Timestamps.Count < maxRequests;

            if (allowed)
                entry.Timestamps.Enqueue(now.ToUnixTimeMilliseconds());

            // ResetsAt: when the oldest in-window request ages out, freeing a slot.
            var resetsAt = entry.Timestamps.Count > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.Timestamps.Peek()).AddSeconds(windowSeconds)
                : now;

            return Task.FromResult(new RateLimitResult(
                IsAllowed: allowed,
                Remaining: Math.Max(0, maxRequests - entry.Timestamps.Count),
                Limit: maxRequests,
                ResetsAt: resetsAt
            ));
        }
    }
}
