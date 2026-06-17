// Azure Functions (isolated worker model, .NET 8+)
// Add this project: dotnet new func --worker-runtime dotnet-isolated -n ApiRateLimiter.Functions
// Add package: Microsoft.Azure.Functions.Worker.Extensions.Timer

using Dapper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiRateLimiter.Functions;

public class CleanupFunction(ILogger<CleanupFunction> logger, IConfiguration config)
{
    // Runs every 5 minutes: "0 */5 * * * *"
    [Function("PurgeExpiredRateLimitCounters")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        var connStr = config.GetConnectionString("RateLimit")
            ?? throw new InvalidOperationException("RateLimit connection string missing.");

        await using var conn = new SqlConnection(connStr);
        var deleted = await conn.ExecuteAsync("dbo.usp_PurgeExpiredCounters",
            commandType: System.Data.CommandType.StoredProcedure);

        logger.LogInformation("Purged {Count} expired rate-limit rows at {Time}.",
            deleted, DateTimeOffset.UtcNow);
    }
}
