# CLAUDE.md — GoogleDocParser (.NET)

## Project Overview

A .NET 8 console application that fetches a publicly-published Google Doc,
parses its table using HTML scraping, and renders the data as a 2D character grid.

The current document encodes pixel-art text using block characters (`█` / `░`)
at x/y coordinates — running the app prints the decoded image to the terminal.

---

## Architecture

```
GoogleDocParser/
├── CLAUDE.md                          ← this file
├── GoogleDocParser.csproj
├── appsettings.json                   ← document URL + log config
├── Program.cs                         ← DI host setup + grid renderer
├── Models/
│   ├── TableRow.cs                    ← 3-column row model
│   └── DocSettings.cs                 ← config binding model
└── Services/
    ├── IDocService.cs                 ← service interface
    └── PublishedDocService.cs         ← HTML fetch + table parser
```

---

## Key Design Decisions

- **Published URL, not API** — the target doc uses Google's "Publish to web" URL
  (`/pub`), which returns public HTML. No Google API credentials are needed.
- **HtmlAgilityPack** — parses the returned HTML to extract `<table>` rows and cells.
- **`IHttpClientFactory` via DI** — `HttpClient` is injected as a typed client through
  `AddHttpClient<IDocService, PublishedDocService>()`, avoiding socket exhaustion from
  `new HttpClient()`.
- **`IConfiguration` injection** — `PublishedDocService` reads `GoogleDoc:DocumentUrl`
  directly from `IConfiguration` to avoid `IOptions<T>` version-skew issues.
- **`UseContentRoot(AppContext.BaseDirectory)`** — ensures `appsettings.json` is found
  in the binary output directory regardless of the working directory at launch.
- **`skipHeader: true` by default** — first table row is always treated as the header.

---

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `HtmlAgilityPack` | 1.12.4 | HTML parsing |
| `Microsoft.Extensions.Hosting` | 8.0.0 | DI host, configuration |
| `Microsoft.Extensions.Http` | 8.0.0 | `IHttpClientFactory` / `AddHttpClient` |

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "GoogleDoc": {
    "DocumentUrl": "https://docs.google.com/document/d/e/YOUR_PUBLISH_TOKEN/pub"
  }
}
```

Replace `DocumentUrl` with any Google Doc published via **File → Share → Publish to web**.

---

## How to Run

```bash
dotnet run --project GoogleDocParser/GoogleDocParser.csproj
```

Expected output — a 7-row block-character grid rendered to the terminal:

```
██░     ██░    ███████░  ██░     ...
██░     ██░  ███░    ██░ ███░    ...
...
```

---

## Core Classes

### `IDocService`
```csharp
Task<List<TableRow>> GetTableDataAsync(bool skipHeader = true);
Task<List<string>> GetSecondColumnOnlyAsync(bool skipHeader = true);
```

### `PublishedDocService : IDocService`

| Method | Description |
|--------|-------------|
| `GetTableDataAsync` | Fetches HTML, parses first `<table>`, returns all rows |
| `GetSecondColumnOnlyAsync` | Returns only `Column2` values from each row |
| `FetchHtmlAsync` | `HttpClient.GetStringAsync` with typed exception handling |
| `GetCellText` | Strips HTML entities and trims whitespace from a `<td>` |

### `TableRow`
```csharp
public string Column1 { get; set; }  // x-coordinate
public string Column2 { get; set; }  // character (█ or ░)
public string Column3 { get; set; }  // y-coordinate
```

---

## Exception Handling

| Scenario | Exception thrown |
|----------|-----------------|
| `DocumentUrl` missing from config | `InvalidOperationException` at startup |
| HTTP request fails | `InvalidOperationException` wrapping `HttpRequestException` |
| Request times out (> 30s) | `InvalidOperationException` wrapping `TaskCanceledException` |
| No `<table>` in fetched HTML | `InvalidOperationException` |
| Non-integer coordinate value | `FormatException` |
| Empty table (0 data rows) | `InvalidOperationException` |

All exceptions are caught in `Program.cs`, printed to `stderr`, and exit with code 1.

---

## Unit and Integration Testing

Test project: `GoogleDocParser.Tests/` (targets `net8.0`, xUnit v2.9.x)

```
GoogleDocParser.Tests/
├── GoogleDocParser.Tests.csproj
├── Helpers/
│   └── FakeHttpMessageHandler.cs     ← configurable stub for HttpMessageHandler
├── Unit/
│   └── PublishedDocServiceTests.cs   ← 14 tests, no network calls
└── Integration/
    └── PublishedDocServiceIntegrationTests.cs  ← 8 tests, WireMock.Net stub server
```

### Running tests

```bash
dotnet test GoogleDocParser.Tests/GoogleDocParser.Tests.csproj
```

### Test approach

| Layer | What's faked | What's real |
|-------|-------------|-------------|
| Unit | `FakeHttpMessageHandler` replaces `HttpClient` transport | HTML parsing, row mapping, exception wrapping |
| Integration | WireMock.Net spins up a local HTTP server | `HttpClient` + full parsing stack |

### Key test packages

| Package | Purpose |
|---------|---------|
| `xunit` 2.9.x | Test framework (provides global `Xunit` usings automatically) |
| `WireMock.Net` | Real HTTP stub server for integration tests |
| `Microsoft.Extensions.Configuration` | `ConfigurationBuilder` / `AddInMemoryCollection` in test setup |
| `coverlet.collector` | Code coverage via `dotnet test --collect:"XPlat Code Coverage"` |

---

## Extending This Project

### Point to a different Google Doc
Update `GoogleDoc:DocumentUrl` in `appsettings.json` — no code changes needed.

### Target a different table (multiple tables in one doc)
In `PublishedDocService.GetTableDataAsync`, change `SelectSingleNode("//table")`
to `SelectNodes("//table")[1]` (0-based index).

### Add more columns
Add properties to `TableRow` and map additional `cells[n]` in `PublishedDocService`.

### Expose as a web API
```csharp
app.MapGet("/api/grid", async (IDocService svc) =>
    await svc.GetTableDataAsync(skipHeader: true));
```
