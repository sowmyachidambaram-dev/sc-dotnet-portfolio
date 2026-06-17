using ApiRateLimiter.Core.Models;

namespace ApiRateLimiter.Core.Interfaces;

public interface IRateLimiter
{
    Task<RateLimitResult> IsAllowedAsync(string clientId, CancellationToken ct = default);
}
