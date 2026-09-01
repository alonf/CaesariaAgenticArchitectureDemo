using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CommandCenter.Web.Services;

/// <summary>
/// Renders model-produced markdown into safe HTML for the agent answer panel. Model output is
/// untrusted: raw HTML is escaped by the pipeline, images are reduced to their alt text (no
/// outbound requests from model output), and link destinations - regular links and autolinks
/// alike - must be absolute http, https, or mailto URLs; anything else (javascript:, data:,
/// file:, relative paths) is reduced to plain text.
/// </summary>
internal static class AnswerHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    /// <summary>
    /// Converts the supplied markdown to sanitized HTML.
    /// </summary>
    /// <param name="markdown">The model-produced markdown text.</param>
    /// <returns>HTML that is safe to render into the page.</returns>
    public static string ToSafeHtml(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            if (link.IsImage || !IsSafeUrl(link.Url))
            {
                // Unwrap to the link's text (an image's children are its alt text).
                link.ReplaceBy(new ContainerInline(), copyChildren: true);
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!autolink.IsEmail && !IsSafeUrl(autolink.Url))
            {
                autolink.ReplaceBy(new LiteralInline(autolink.Url));
            }
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Converts model-produced markdown to plain text. Used where an answer is stored rather than
    /// rendered - case memory is recalled into a plain-text view precisely because stored text is
    /// untrusted, so markup left in it shows up verbatim as <c>**on**</c> on the projector.
    /// </summary>
    /// <param name="markdown">The model-produced markdown text.</param>
    /// <returns>The text with markup removed and whitespace collapsed.</returns>
    public static string ToPlainText(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var plain = Markdown.ToPlainText(markdown, Pipeline);
        return string.Join(' ', plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && parsed.Scheme is "http" or "https" or "mailto";
}
