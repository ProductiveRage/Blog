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
			.Build();

		public static string ToHtml(string markdown)
		{
			if (markdown == null)
				throw new ArgumentNullException("text");

			return Markdown.ToHtml(markdown, _markdigPipeline);
		}
	}
}