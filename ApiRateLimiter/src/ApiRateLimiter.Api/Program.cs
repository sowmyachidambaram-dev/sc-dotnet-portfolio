using ApiRateLimiter.Api.Middleware;
using ApiRateLimiter.Core.Interfaces;
using ApiRateLimiter.Core.Models;
using ApiRateLimiter.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

// Swap FixedWindowRateLimiter for SqlRateLimiter when running on multiple nodes.
builder.Services.AddSingleton<IRateLimiter, FixedWindowRateLimiter>();

var app = builder.Build();

app.UseMiddleware<RateLimitMiddleware>();
app.MapControllers();
app.Run();
