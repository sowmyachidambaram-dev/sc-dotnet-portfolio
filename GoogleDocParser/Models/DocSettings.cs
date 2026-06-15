namespace GoogleDocParser.Models;

/// <summary>
/// Strongly-typed binding model for the <c>GoogleDoc</c> section in appsettings.json.
/// </summary>
public class DocSettings
{
    /// <summary>
    /// The publicly-accessible "Publish to web" URL of the Google Doc.
    /// Example: <c>https://docs.google.com/document/d/e/&lt;TOKEN&gt;/pub</c>
    /// </summary>
    public string DocumentUrl { get; set; } = string.Empty;
}
