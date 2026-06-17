using ApiRateLimiter.Core.Interfaces;
using ApiRateLimiter.Core.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ApiRateLimiter.Api.Infrastructure;

// SQL Server implementation: atomicity is guaranteed by the MERGE stored procedure.
// Use this when the API runs on multiple nodes (no shared in-memory state).
public sealed class SqlRateLimiter : IRateLimiter
{
    private readonly string _connectionString;
    private readonly RateLimitOptions _options;

    public SqlRateLimiter(IConfiguration config, IOptions<RateLimitOptions> options)
    {
        _connectionString = config.GetConnectionString("RateLimit")
            ?? throw new InvalidOperationException("RateLimit connection string is missing.");
        _options = options.Value;
    }

    public async Task<RateLimitResult> IsAllowedAsync(string clientId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        var row = await conn.QuerySingleAsync<CounterRow>(
            "dbo.usp_CheckAndIncrement",
            new { ClientId = clientId, WindowSeconds = _options.WindowSeconds, _options.Limit },
            commandType: System.Data.CommandType.StoredProcedure
        );

        return new RateLimitResult(
            IsAllowed: row.RequestCount <= _options.Limit,
            Remaining: Math.Max(0, _options.Limit - row.RequestCount),
            Limit: _options.Limit,
            ResetsAt: row.ExpiresAt
        );
    }

    private record CounterRow(int RequestCount, DateTimeOffset ExpiresAt);
}
