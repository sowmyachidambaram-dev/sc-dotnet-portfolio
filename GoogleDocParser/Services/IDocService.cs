using GoogleDocParser.Models;

namespace GoogleDocParser.Services;

/// <summary>Defines the contract for fetching and parsing tabular data from a Google Doc.</summary>
public interface IDocService
{
    /// <summary>
    /// Fetches the first HTML table from the document and maps each row to a <see cref="TableRow"/>.
    /// </summary>
    /// <param name="skipHeader">
    /// When <c>true</c> (default), the first row is treated as a header and excluded from the result.
    /// </param>
    /// <returns>A list of <see cref="TableRow"/> objects representing the data rows.</returns>
    Task<List<TableRow>> GetTableDataAsync(bool skipHeader = true);

    /// <summary>Returns only the <see cref="TableRow.Column2"/> value from each data row.</summary>
    /// <param name="skipHeader">
    /// When <c>true</c> (default), the first row is treated as a header and excluded.
    /// </param>
    /// <returns>A list of strings, one entry per data row.</returns>
    Task<List<string>> GetSecondColumnOnlyAsync(bool skipHeader = true);
}
