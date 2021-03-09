using System;
using Markdig;

namespace BlogBackEnd.Models
{
    /// <summary>
    /// Helper class for transforming Markdown into HTML
    /// </summary>
    public static class MarkdownTransformations
	{
		private static readonly MarkdownPipeline _markdigPipeline = new MarkdownPipelineBuilder()
			.UseSoftlineBreakAsHardlineBreak()
			.UsePipeTables()
			.Build();

		public static string ToHtml(string markdown)
		{
			if (markdown == null)
				throw new ArgumentNullException(nameof(markdown));

			return Markdown.ToHtml(markdown, _markdigPipeline);
		}
	}
}