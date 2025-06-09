using System.Web;
using HtmlAgilityPack;
using Markdig;

namespace GenerateSimilarityEmbeddings;

public static class BlogPostReader
{
    private static readonly MarkdownPipeline _markdigPipeline = new MarkdownPipelineBuilder()
        .UseSoftlineBreakAsHardlineBreak()
        .UsePipeTables()
        .Build();

    public static async IAsyncEnumerable<BlogPost> Read(IEnumerable<FileInfo> files, bool removeCodeBlocks = true)
    {
        foreach (var file in files)
        {
            if (!file.Exists || !int.TryParse(file.Name.Split(',')[0], out var id))
            {
                continue;
            }

            var html = Markdown.ToHtml(await file.OpenText().ReadToEndAsync(), _markdigPipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Semantic search tends to work best with English language content, and including blocks of code may negatively impact the
            // embeddings that are generated (eg. code may include non-English keywords, is not likely to follow a sentence structure,
            // may often have reference names that are as portmanteaus - eg. "isAllowedToLogIn" as opposed to the individual words "is",
            // "allowed", "to", "log", "in").
            //
            // Arguably, the might be content within comments in code blocks that it would make sense to include, so the remove-all-code-
            // blocks functionality is optional, in case it's worth exploring.
            if (removeCodeBlocks)
            {
                var codeBlocks = doc.DocumentNode.SelectNodes("//pre/code");
                if (codeBlocks is not null)
                {
                    foreach (var codeBlock in codeBlocks)
                    {
                        if (!codeBlock.InnerHtml.Contains('\n'))
                        {
                            throw new Exception("Don't expect <pre> tags in inline code content");
                        }
                        codeBlock.ParentNode.Remove();
                    }
                }
            }

            // Replace any "<a href=whatever>The Thing</a>" tags with the text inside ("The Thing", in that example) - partly
            // because URIs are less likely to be English language content that it makes sense to consider semantically, and in
            // part to sidestep the complication that there is support in the "Blog" project for links of the form "Post{x}" to
            // appear in the markdown, which would be expanded out into the full url for the post whose id matches {x}.
            var links = doc.DocumentNode.SelectNodes("//a");
            if (links is not null)
            {
                foreach (var link in links)
                {
                    var textElement = doc.CreateTextNode(link.InnerText);
                    link.ParentNode.ReplaceChild(textElement, link);
                }
            }

            var lines = HttpUtility.HtmlDecode(doc.DocumentNode.InnerText)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToArray();

            yield return new(id, lines[0], string.Join('\n', lines.Skip(1).SkipWhile(string.IsNullOrWhiteSpace)));
        }
    }
}