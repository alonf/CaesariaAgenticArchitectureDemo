using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CommandCenter.Web.Services;

/// <summary>
/// Renders model-produced markdown into safe HTML for the agent answer panel. Model output is
/// untrusted: raw HTML is escaped by the pipeline, and link destinations are restricted to
/// http/https/mailto - anything else (for example javascript:) is stripped down to its text.
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

        foreach (var link in document.Descendants<LinkInline>().Where(link => !IsSafeUrl(link.Url)))
        {
            link.Url = string.Empty;
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    private static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            ? parsed.Scheme is "http" or "https" or "mailto"
            : url is not null && !url.Contains(':', StringComparison.Ordinal);
}
