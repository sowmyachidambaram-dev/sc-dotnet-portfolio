using ApiRateLimiter.Core.Models;
using ApiRateLimiter.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiRateLimiter.Tests;

public class FixedWindowRateLimiterTests
{
    private static FixedWindowRateLimiter Build(int limit = 5, int windowSeconds = 60)
    {
        var opts = Options.Create(new RateLimitOptions { Limit = limit, WindowSeconds = windowSeconds });
        return new FixedWindowRateLimiter(opts);
    }

    [Fact]
    public async Task Allows_requests_within_limit()
    {
        var limiter = Build(limit: 3);
        for (int i = 0; i < 3; i++)
        {
            var result = await limiter.IsAllowedAsync("client1");
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task Blocks_request_exceeding_limit()
    {
        var limiter = Build(limit: 3);                              
        for (int i = 0; i < 3; i++)
            await limiter.IsAllowedAsync("client1");

        var blocked = await limiter.IsAllowedAsync("client1");
        Assert.False(blocked.IsAllowed);
        Assert.Equal(0, blocked.Remaining);
    }

    [Fact]
    public async Task Different_clients_have_independent_buckets()
    {
        var limiter = Build(limit: 1);
        var r1 = await limiter.IsAllowedAsync("alice");
        var r2 = await limiter.IsAllowedAsync("bob");

        Assert.True(r1.IsAllowed);
        Assert.True(r2.IsAllowed);
    }

    [Fact]
    public async Task Concurrent_requests_never_exceed_limit()
    {
        const int limit = 10;
        const int threads = 50;
        var limiter = Build(limit: limit);

        var results = await Task.WhenAll(
            Enumerable.Range(0, threads)
                      .Select(_ => limiter.IsAllowedAsync("shared-client"))
        );

        int allowed = results.Count(r => r.IsAllowed);
        Assert.Equal(limit, allowed);
    }

    [Fact]
    public async Task Remaining_count_decrements_correctly()
    {
        var limiter = Build(limit: 5);
        for (int i = 5; i >= 1; i--)
        {
            var result = await limiter.IsAllowedAsync("client1");
            Assert.Equal(i - 1, result.Remaining);
        }
    }
}
