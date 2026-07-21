using STRAIBot.Services.Text;

namespace STRAIBot.Tests.Services.Text;

public class HtmlMessageCleanerTests
{
    private readonly HtmlMessageCleaner _sut = new();

    [Fact]
    public void Clean_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Clean(null));
        Assert.Equal(string.Empty, _sut.Clean("   "));
    }

    [Fact]
    public void Clean_RemovesHtmlTagsAndDecodesEntities()
    {
        var html = "<div>Hello &amp; welcome<br/>to <b>CozyCrab</b>!</div>";

        var result = _sut.Clean(html);

        Assert.Contains("Hello & welcome", result);
        Assert.Contains("to CozyCrab!", result);
        Assert.DoesNotContain("<div>", result);
        Assert.DoesNotContain("<b>", result);
    }

    [Fact]
    public void Clean_TrimsSignatureBlock()
    {
        var html = "<p>Main message line</p><p>-- </p><p>Sent from my iPhone</p>";

        var result = _sut.Clean(html);

        Assert.Equal("Main message line", result);
    }

    [Fact]
    public void Clean_CollapsesExcessBlankLines()
    {
        var html = "<div>Line1</div><div></div><div></div><div>Line2</div>";

        var result = _sut.Clean(html);

        Assert.Contains("Line1", result);
        Assert.Contains("Line2", result);
        Assert.DoesNotContain("\n\n\n", result);
    }
}
