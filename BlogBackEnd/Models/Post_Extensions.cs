using System;
using System.Linq;
using System.Text;
using System.Web;
using HeyRed.MarkdownSharp;
using HtmlAgilityPack;

namespace BlogBackEnd.Models
{
	public static class Post_Extensions
	{
		/// <summary>
		/// This will transform the MarkdownContent into html and then transform it down to plain text. Any html-encoded content will no longer be html-encoded; it
		/// will be the content that was escaped to display as html. Whitespace will be normalised such that all whitespace is replaced with individual spaces and
		/// then any runs of spaces reduced down to a single space. This means that formatting will be lost. Finally, if the start of the content matches the Title
		/// property of the Post, this will be stripped out.
		/// </summary>
		public static string GetContentAsPlainText(this Post source)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			var doc = new HtmlDocument();
			doc.LoadHtml(
				(new Markdown()).Transform(source.MarkdownContent)
			);
			var segments = doc.DocumentNode.DescendantNodesAndSelf()
				.Where(node => node.NodeType == HtmlNodeType.Text)
				.Select(node => node.InnerText);

			// The Markdown content will generally have the inner text html encoded so although we've extracted it as plain text we'll
			// still need to HtmlDecode it further to get back the triangular brackets (for example)
			var whitespaceNormalisedContentBuilder = new StringBuilder();
			var lastCharacterWasWhitespace = false;
			foreach (var character in HttpUtility.HtmlDecode(string.Join(" ", segments)).Trim())
			{
				if (char.IsWhiteSpace(character))
				{
					if (!lastCharacterWasWhitespace)
					{
						whitespaceNormalisedContentBuilder.Append(" ");
						lastCharacterWasWhitespace = true;
					}
					continue;
				}
				whitespaceNormalisedContentBuilder.Append(character);
				lastCharacterWasWhitespace = false;
			}

			// The content often includes the title at the start which we don't want to display (since here we're rendering the post
			// content only), so it needs removing if this is the case
			var content = whitespaceNormalisedContentBuilder.ToString();
			if (content.StartsWith(source.Title.Trim()))
				content = content.Substring(source.Title.Trim().Length).Trim();

			return content;
		}
	}
}
