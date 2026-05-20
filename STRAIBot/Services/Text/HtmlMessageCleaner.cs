using System.Net;
using System.Text.RegularExpressions;

namespace STRAIBot.Services.Text;

/// <summary>
/// Cleans HTML guest message bodies into plain text.
/// No external dependencies — uses only BCL regex and WebUtility.
/// </summary>
public partial class HtmlMessageCleaner : IHtmlMessageCleaner
{
    public string Clean(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return string.Empty;

        var text = rawBody;

        // ── 1. Convert block-level tags to newlines before stripping ─────────
        text = BlockTagNewlineRegex().Replace(text, "\n");

        // ── 2. Strip all remaining HTML tags ─────────────────────────────────
        text = HtmlTagRegex().Replace(text, string.Empty);

        // ── 3. Decode HTML entities (&amp; &lt; &nbsp; &#160; etc.) ──────────
        text = WebUtility.HtmlDecode(text);

        // ── 4. Remove common email signature noise ────────────────────────────
        //    Best-effort: cuts at the first line that looks like a signature marker.
        text = TrimSignature(text);

        // ── 5. Collapse excessive whitespace / blank lines ────────────────────
        text = MultipleBlankLinesRegex().Replace(text, "\n\n");
        text = text.Trim();

        return text;
    }

    private static string TrimSignature(string text)
    {
        // Common markers that signal the start of an email signature block.
        var signatureMarkers = new[]
        {
            "\n--\n", "\n-- \n",       // RFC 3676 sig delimiter
            "\nSent from my ",         // mobile footers
            "\nGet Outlook",           // Outlook mobile footer
            "\n________________________________", // Outlook horizontal rule
            "\nOn ", // "On Mon, 12 Jan ... wrote:" reply quote header
        };

        foreach (var marker in signatureMarkers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                text = text[..idx];
        }

        return text;
    }

    // Replace <br>, <br/>, <p>, </p>, <div>, </div>, <li> etc. with newlines
    [GeneratedRegex(@"<(br\s*/?|/?(p|div|li|tr|h[1-6]|blockquote))[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagNewlineRegex();

    // Strip any remaining tags
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    // Collapse 3+ consecutive newlines to 2
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLinesRegex();
}
