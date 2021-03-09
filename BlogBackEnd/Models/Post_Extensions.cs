using System;
using System.Linq;
using System.Text;
using System.Web;
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
				throw new ArgumentNullException(nameof(source));

			var doc = new HtmlDocument();
			doc.LoadHtml(
				MarkdownTransformations.ToHtml(source.MarkdownContent)
			);
			var segments = doc.DocumentNode.DescendantsAndSelf()
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
						whitespaceNormalisedContentBuilder.Append(' ');
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
				content = content[source.Title.Trim().Length..].Trim();

			return content;
		}

		private static readonly char[] _allWhitespaceCharacters = Enumerable.Range(char.MinValue, char.MaxValue).Select(value => (char)value).Where(char.IsWhiteSpace).ToArray();

		/// <summary>
		/// Try to return plain text from the first paragraph element that exists in a Post's MarkdownContent. The length may optionally be limited (if the content
		/// must be reduced to meet this requirement then ellipses characters will be appended to the end). If there is no paragraph element (as a direct child node
		/// of the content) or if that paragraph is empty then this will return null, it will never return a blank string nor one with any leading or trailing
		/// whitespace).
		/// </summary>
		public static string TryToGetFirstParagraphContentAsPlainText(this Post source, int maxLength = int.MaxValue)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (maxLength < 3)
				throw new ArgumentException("Maximum length must be AT LEAST 3 because we'll add two '..' characters to any string that is trimmed, which would leave only a single character from the input!");

			var doc = new HtmlDocument();
			doc.LoadHtml(
				MarkdownTransformations.ToHtml(source.MarkdownContent)
			);
			var firstParagraph = doc.DocumentNode.ChildNodes.FindFirst("p");
			if (firstParagraph == null)
				return null;

			var text = (firstParagraph.InnerText ?? "").Trim();
			if (text == "")
				return null;

			if (text.Length < maxLength)
				return text;

			var whiteSpaceBreak = text.LastIndexOfAny(_allWhitespaceCharacters, startIndex: maxLength - 2);
			if (whiteSpaceBreak != -1)
				return text.Substring(0, whiteSpaceBreak) + "..";
			return text.Substring(0, maxLength - 2) + "..";
		}
	}
}