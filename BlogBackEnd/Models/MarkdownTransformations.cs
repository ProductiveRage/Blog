﻿using System;
using HtmlAgilityPack;
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

			var html = Markdown.ToHtml(markdown, _markdigPipeline);

			// Populate the "title" attribute on any img nodes that don't have one but DO have an "alt text" value (could have done this as
			// a variation on the Markdig LinkInlineParser but this way seemed easier! It also has the benefit that it will apply to any img
			// tags that are included are bare html, rather than via markdown - for cases where additional classes are added, for example).
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var imgNodes = doc.DocumentNode.SelectNodes("//img");
			if (imgNodes is not null)
			{
				foreach (var node in imgNodes)
				{
					var titleAttribute = node.Attributes["title"];
					if (!string.IsNullOrWhiteSpace(titleAttribute?.Value))
						continue;
					
					var altTextAttribute = node.Attributes["alt"];
					if (string.IsNullOrWhiteSpace(altTextAttribute?.Value))
						continue;

					node.Attributes.Add("title", altTextAttribute?.Value);
				}
				html = doc.DocumentNode.OuterHtml;
			}

			return html;
		}
	}
}