namespace ApiRateLimiter.Core.Models;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int Limit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
