using CommandCenter.Web.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class AnswerHtmlTests
{
    [Fact]
    public void RendersEmphasisHeadingsAndLists()
    {
        var html = AnswerHtml.ToSafeHtml("## Verdict\n**L-417** is on.\n- manual override\n- daylight");

        Assert.Contains("<h2>Verdict</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>L-417</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<li>manual override</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersPipeTables()
    {
        var html = AnswerHtml.ToSafeHtml("| Field | Value |\n| --- | --- |\n| Reported | On |");

        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Field</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>On</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesRawHtml()
    {
        // Model output is untrusted: markup must never reach the page as live HTML.
        var html = AnswerHtml.ToSafeHtml("Before <script>alert('x')</script> <img src=x onerror=alert(1)> after");

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StripsUnsafeLinkSchemes()
    {
        var html = AnswerHtml.ToSafeHtml("[click](javascript:alert(1)) and [ok](https://example.com)");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"https://example.com\"", html, StringComparison.Ordinal);
    }
}
