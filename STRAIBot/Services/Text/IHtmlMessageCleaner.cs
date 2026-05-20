namespace STRAIBot.Services.Text;

/// <summary>
/// Converts an HTML guest message body (as sent by Guesty) into clean plain text
/// suitable for classification and AI decision processing.
/// </summary>
public interface IHtmlMessageCleaner
{
    /// <summary>
    /// Strips HTML tags, decodes HTML entities, and removes common email
    /// signature noise. Returns clean plain text.
    /// Returns the original string unchanged if it contains no HTML.
    /// </summary>
    string Clean(string? rawBody);
}
