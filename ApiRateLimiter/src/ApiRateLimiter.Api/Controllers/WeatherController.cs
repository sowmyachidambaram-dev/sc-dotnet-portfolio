using Microsoft.AspNetCore.Mvc;

namespace ApiRateLimiter.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private static readonly string[] Summaries =
        ["Freezing", "Cold", "Mild", "Warm", "Hot"];

    [HttpGet]
    public IActionResult Get()
    {
        var forecast = Enumerable.Range(1, 5).Select(i => new
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)),
            TemperatureC = Random.Shared.Next(-10, 40),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });
        return Ok(forecast);
    }
}
