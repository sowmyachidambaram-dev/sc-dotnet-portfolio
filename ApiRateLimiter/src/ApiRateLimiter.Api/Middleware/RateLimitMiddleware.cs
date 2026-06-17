using ApiRateLimiter.Core.Interfaces;

namespace ApiRateLimiter.Api.Middleware;

public sealed class RateLimitMiddleware(RequestDelegate next, IRateLimiter rateLimiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = ResolveClientId(context);
        var result = await rateLimiter.IsAllowedAsync(clientId, context.RequestAborted);

        context.Response.Headers["X-RateLimit-Limit"]     = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"]     = result.ResetsAt.ToUnixTimeSeconds().ToString();

        if (!result.IsAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] =
                ((int)(result.ResetsAt - DateTimeOffset.UtcNow).TotalSeconds).ToString();
            await context.Response.WriteAsync("Rate limit exceeded. Please retry later.");
            return;
        }

        await next(context);
    }

    private static string ResolveClientId(HttpContext context)
    {
        // Prefer an authenticated identity; fall back to forwarded IP, then direct IP.
        if (context.User.Identity?.IsAuthenticated == true)
            return context.User.Identity.Name!;

        return context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
            ? forwarded.ToString().Split(',')[0].Trim()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
