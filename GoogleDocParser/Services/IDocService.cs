using GoogleDocParser.Models;

namespace GoogleDocParser.Services;

public interface IDocService
{
    Task<List<TableRow>> GetTableDataAsync(bool skipHeader = true);
    Task<List<string>> GetSecondColumnOnlyAsync(bool skipHeader = true);
}
