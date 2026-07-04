using Markdig;

namespace SciFiCharacterChat.Services;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .Build();

    public static string ToHtml(string markdown) => Markdown.ToHtml(markdown, Pipeline);
}