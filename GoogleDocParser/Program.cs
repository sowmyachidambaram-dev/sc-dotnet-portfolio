using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GoogleDocParser.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((_, services) =>
    {
        services.AddHttpClient<IDocService, PublishedDocService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    })
    .Build();

try
{
    var service = host.Services.GetRequiredService<IDocService>();
    var rows = await service.GetTableDataAsync(skipHeader: true);

    if (rows.Count == 0)
        throw new InvalidOperationException("Document table is empty.");

    var points = rows.Select(r =>
    {
        if (!int.TryParse(r.Column1, out int x))
            throw new FormatException($"Invalid x-coordinate: '{r.Column1}'");
        if (!int.TryParse(r.Column3, out int y))
            throw new FormatException($"Invalid y-coordinate: '{r.Column3}'");
        return (x, ch: r.Column2, y);
    }).ToList();

    int maxX = points.Max(p => p.x);
    int maxY = points.Max(p => p.y);

    var grid = new string[maxY + 1, maxX + 1];
    for (int y = 0; y <= maxY; y++)
        for (int x = 0; x <= maxX; x++)
            grid[y, x] = " ";

    foreach (var (x, ch, y) in points)
        grid[y, x] = ch;

    for (int y = 0; y <= maxY; y++)
    {
        for (int x = 0; x <= maxX; x++)
            Console.Write(grid[y, x]);
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
