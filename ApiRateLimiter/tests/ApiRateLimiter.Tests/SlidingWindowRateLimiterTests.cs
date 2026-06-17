using ApiRateLimiter.Core.Services;
using Xunit;

namespace ApiRateLimiter.Tests;

public class SlidingWindowRateLimiterTests
{
    private static SlidingWindowRateLimiter Build() => new();

    [Fact]
    public async Task Allows_requests_within_limit()
    {
        var limiter = Build();
        for (int i = 0; i < 3; i++)
        {
            var result = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task Blocks_request_at_limit()
    {
        var limiter = Build();
        for (int i = 0; i < 3; i++)
            await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);

        var blocked = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);
        Assert.False(blocked.IsAllowed);
        Assert.Equal(0, blocked.Remaining);
    }

    [Fact]
    public async Task Remaining_decrements_with_each_request()
    {
        var limiter = Build();
        for (int expected = 4; expected >= 0; expected--)
        {
            var result = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 5);
            Assert.Equal(expected, result.Remaining);
        }
    }

    [Fact]
    public async Task Different_clients_are_independent()
    {
        var limiter = Build();
        var alice = await limiter.IsAllowedAsync("alice", windowSeconds: 60, maxRequests: 1);
        var bob   = await limiter.IsAllowedAsync("bob",   windowSeconds: 60, maxRequests: 1);

        Assert.True(alice.IsAllowed);
        Assert.True(bob.IsAllowed);
    }

    [Fact]
    public async Task Blocked_request_is_not_counted()
    {
        var limiter = Build();
        for (int i = 0; i < 3; i++)
            await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);

        // Two blocked requests
        await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);
        await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);

        // Remaining should still be 0, not negative
        var result = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 3);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task Resets_at_is_oldest_timestamp_plus_window()
    {
        var limiter = Build();
        var before = DateTimeOffset.UtcNow;
        await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 5);
        var result = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 5);
        var after = DateTimeOffset.UtcNow;

        // ResetsAt must be ~60 s after the first request was recorded.
        // Subtract 1 ms on the lower bound to absorb Unix-ms truncation of the stored timestamp.
        Assert.InRange(result.ResetsAt, before.AddSeconds(60).AddMilliseconds(-1), after.AddSeconds(60));
    }

    [Fact]
    public async Task Allows_request_after_window_expires()
    {
        var limiter = Build();

        // Fill up the limit with a 1-second window
        for (int i = 0; i < 3; i++)
            await limiter.IsAllowedAsync("client1", windowSeconds: 1, maxRequests: 3);

        var blocked = await limiter.IsAllowedAsync("client1", windowSeconds: 1, maxRequests: 3);
        Assert.False(blocked.IsAllowed);

        // Wait for the window to expire
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        var allowed = await limiter.IsAllowedAsync("client1", windowSeconds: 1, maxRequests: 3);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public async Task Concurrent_requests_never_exceed_limit()
    {
        const int limit = 10;
        const int threads = 50;
        var limiter = Build();

        var results = await Task.WhenAll(
            Enumerable.Range(0, threads)
                      .Select(_ => limiter.IsAllowedAsync("shared-client", windowSeconds: 60, maxRequests: limit))
        );

        int allowed = results.Count(r => r.IsAllowed);
        Assert.Equal(limit, allowed);
    }

    [Fact]
    public async Task Limit_is_echoed_in_result()
    {
        var limiter = Build();
        var result = await limiter.IsAllowedAsync("client1", windowSeconds: 60, maxRequests: 7);
        Assert.Equal(7, result.Limit);
    }
}
