using System;
using System.Linq;
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
			.UseAutoIdentifiers() // This adds ids to the headers but doesn't have an option to inject anchors into them to make them clickable
			.UseSoftlineBreakAsHardlineBreak()
			.UsePipeTables()
			.Build();

		public static string ToHtml(string markdown, string postSlug)
		{
			if (markdown == null)
				throw new ArgumentNullException(nameof(markdown));

			var html = Markdown.ToHtml(markdown, _markdigPipeline);

			// For header (h3, h4, etc..) tags that have an id generated via the call to UseAutoIdentifiers when the pipeline is configured,
			// ensure that there is a link in the header so that it can be clicked and the URL updated to set the hash tag to the current
			// header (to make sharing links to particular sections of articles easier)
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var nodesWithId = doc.DocumentNode.SelectNodes("//@id");
			if (nodesWithId is not null)
			{
				foreach (var node in nodesWithId)
				{
					if (!node.Name.StartsWith("h", StringComparison.OrdinalIgnoreCase) || !int.TryParse(node.Name[1..], out _))
						continue;

					// If there's already an anchor in the header then presume that it's already been configured to link to somewhere intentionally
					// (like the way in which the post headers do via the PostHelper class' ReplaceFirstLineHashHeaderWithPostLink method)
					if (node.ChildNodes.Any(childNode => childNode.Name.Equals("a", StringComparison.OrdinalIgnoreCase)))
						continue;

					// Note: If clicking a header on the home page then don't just add the hash to the URL because that link would become invalid
					// if the home page started showing a later month in the future, so link to the post AND the hash (browsers - Chrome, at least -
					// don't reload the page if you're already on the same URL but with a different hash, so there will be no additional page views
					// recorded in cases where header links are clicked when already reading a single post)
					var newLink = doc.CreateElement("a");
					newLink.Attributes.Add("href", $"/{postSlug}#{node.Id}");
					foreach (var childNode in node.ChildNodes.ToArray()) // ToArray the list so that it's not modified as they are removed
					{
						newLink.AppendChild(childNode.Clone());
						childNode.Remove();
					}
					node.AppendChild(newLink);
				}
				html = doc.DocumentNode.OuterHtml;
			}

			// Populate the "title" attribute on any img nodes that don't have one but DO have an "alt text" value (could have done this as
			// a variation on the Markdig LinkInlineParser but this way seemed easier! It also has the benefit that it will apply to any img
			// tags that are included are bare html, rather than via markdown - for cases where additional classes are added, for example).
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

			// When a table is too wide for mobile (there are no points in the content that can be split on while still making it legible)
			// then horizontal scrolling is going to be required - this isn't really possible with a table element, so wrap every table in
			// a special div that can be used to enforce the scrolling if needed
			var tableNodes = doc.DocumentNode.SelectNodes("//table");
			if (tableNodes is not null)
			{
				foreach (var node in tableNodes)
				{
					var wrapper = doc.CreateElement("div");
					wrapper.SetAttributeValue("class", "TableScrollWrapper");
					node.ParentNode.InsertBefore(wrapper, node);
					wrapper.AppendChild(node.Clone());
					node.Remove();
				}
				html = doc.DocumentNode.OuterHtml;
			}

			return html;
		}
	}
}