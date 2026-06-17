namespace ApiRateLimiter.Core.Models;

public record RateLimitResult(
    bool IsAllowed,
    int Remaining,
    int Limit,
    DateTimeOffset ResetsAt
);
