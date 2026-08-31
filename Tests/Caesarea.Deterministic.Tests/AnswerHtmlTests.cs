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

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    public void UnsafeLinkSchemesAreReducedToPlainText(string url)
    {
        // Both markdown forms must be neutralized: [text](url) links and <url> autolinks.
        var html = AnswerHtml.ToSafeHtml($"[click]({url}) and <{url}>");

        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("click", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeLinksKeepTheirDestinations()
    {
        var html = AnswerHtml.ToSafeHtml("[ok](https://example.com) and <https://example.org> and <ops@example.com>");

        Assert.Contains("href=\"https://example.com\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.org\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"mailto:ops@example.com\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeLinksAreReducedToPlainText()
    {
        // Model output may not steer the operator to arbitrary paths on the demo host.
        var html = AnswerHtml.ToSafeHtml("[admin](/api/admin/reset)");

        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admin", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ImagesAreReducedToAltText()
    {
        // Model output must never trigger outbound requests (tracking pixels, external content).
        var html = AnswerHtml.ToSafeHtml("Before ![status chart](https://example.com/pixel.png) after");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pixel.png", html, StringComparison.Ordinal);
        Assert.Contains("status chart", html, StringComparison.Ordinal);
    }
}
