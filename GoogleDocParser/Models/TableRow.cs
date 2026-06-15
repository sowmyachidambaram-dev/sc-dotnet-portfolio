namespace GoogleDocParser.Models;

/// <summary>Represents a single data row from the parsed Google Doc table.</summary>
/// <remarks>
/// In the default document schema: Column1 = x-coordinate, Column2 = character to render,
/// Column3 = y-coordinate.
/// </remarks>
public class TableRow
{
    /// <summary>First column value — x-coordinate in the default schema.</summary>
    public string Column1 { get; set; } = string.Empty;

    /// <summary>Second column value — character to render in the default schema.</summary>
    public string Column2 { get; set; } = string.Empty;

    /// <summary>Third column value — y-coordinate in the default schema.</summary>
    public string Column3 { get; set; } = string.Empty;
}
