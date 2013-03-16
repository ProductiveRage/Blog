using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using HtmlAgilityPack;

namespace Blog.Helpers.Posts
{
	public static class PostHelper
	{
		public static IHtmlString RenderPost(this HtmlHelper helper, Post post, ICache cache)
		{
			if (helper == null)
				throw new ArgumentNullException("helper");
			if (post == null)
				throw new ArgumentNullException("post");
			if (cache == null)
				throw new ArgumentNullException("cache");

			var cacheKey = "PostHelper-RenderPost-" + post.Id;
			var cachedData = cache[cacheKey];
			if (cachedData != null)
			{
				var cachedPostContent = cachedData as CachablePostContent;
				if ((cachedPostContent != null) && (cachedPostContent.LastModified >= post.LastModified))
					return (IHtmlString)MvcHtmlString.Create(cachedPostContent.RenderableContent);
				cache.Remove(cacheKey);
			}

			var content = GetRenderableContent(helper, post, null, true, true, true);
			cache[cacheKey] = new CachablePostContent(content, post.LastModified);
			return (IHtmlString)MvcHtmlString.Create(content);
		}

		public static IHtmlString RenderPostForRSS(this HtmlHelper helper, Post post, string protocolForAbsoluteUrls, ICache cache)
		{
			if (helper == null)
				throw new ArgumentNullException("helper");
			if (post == null)
				throw new ArgumentNullException("post");
			if (string.IsNullOrWhiteSpace(protocolForAbsoluteUrls))
				throw new ArgumentException("Null/blank protocolForAbsoluteUrls specified");
			if (cache == null)
				throw new ArgumentNullException("cache");

			var cacheKey = "PostHelper-RenderPostForRSS-" + post.Id;
			var cachedData = cache[cacheKey];
			if (cachedData != null)
			{
				var cachedPostContent = cachedData as CachablePostContent;
				if ((cachedPostContent != null) && (cachedPostContent.LastModified >= post.LastModified))
					return (IHtmlString)MvcHtmlString.Create(cachedPostContent.RenderableContent);
				cache.Remove(cacheKey);
			}

			// TODO: Ensure that image urls are absolute
			var content = GetRenderableContent(helper, post, protocolForAbsoluteUrls, false, false, false);
			cache[cacheKey] = new CachablePostContent(content, post.LastModified);
			return (IHtmlString)MvcHtmlString.Create(content);
		}

		private static string GetRenderableContent(HtmlHelper helper, Post post, string optionalProtocolForAbsoluteUrls, bool includeTitle, bool includePostedDate, bool includeTags)
		{
			if (helper == null)
				throw new ArgumentNullException("helper");
			if (post == null)
				throw new ArgumentNullException("post");
			if ((optionalProtocolForAbsoluteUrls != null) && string.IsNullOrWhiteSpace(optionalProtocolForAbsoluteUrls))
				throw new ArgumentException("optionalProtocolForAbsoluteUrls must not be blank if it is non-null");

			var markdownContent = HandlePostLinks(
				helper,
				includeTitle ? ReplaceFirstLineHashHeaderWithPostLink(post.MarkdownContent, post.Id) : RemoveTitle(post.MarkdownContent),
				optionalProtocolForAbsoluteUrls
			);

			var content = new StringBuilder();
			if (includePostedDate)
				content.AppendFormat("<h3 class=\"PostDate\">{0}</h3>", post.Posted.ToString("d MMMM yyyy"));
			content.Append(
				MarkdownHelper.TransformIntoHtml(markdownContent)
			);
			if (includePostedDate)
				content.AppendFormat("<p class=\"PostTime\">Posted at {0}</p>", post.Posted.ToString("HH:mm"));
			if (includeTags && (post.Tags.Any()))
			{
				content.Append("<div class=\"Tags\">");
				content.Append("<label>Tags:</label>");
				content.Append("<ul>");
				foreach (var tag in post.Tags)
				{
					content.AppendFormat(
						"<li>{0}</li>",
						helper.ActionLink(tag, "ArchiveByTag", "ViewPost", new { Tag = tag }, optionalProtocolForAbsoluteUrls)
					);
				}
				content.Append("</ul>");
				content.Append("</div>");
			}

			return content.ToString();
		}

		/// <summary>
		/// Update any markdown links where the target is of the form "Post{0}" to replace with the real url
		/// </summary>
		private static string HandlePostLinks(HtmlHelper helper, string content, string optionalProtocolForAbsoluteUrls)
		{
			if (helper == null)
				throw new ArgumentNullException("helper");
			if (string.IsNullOrEmpty(content))
				return content;
			if ((optionalProtocolForAbsoluteUrls != null) && string.IsNullOrWhiteSpace(optionalProtocolForAbsoluteUrls))
				throw new ArgumentException("optionalProtocolForAbsoluteUrls must not be blank if it is non-null");

			var url = new UrlHelper(helper.ViewContext.RequestContext);
			return Regex.Replace(
				content,
				@"\]\(Post(\d+)\)",
				delegate(Match match)
				{
					return String.Format(
						"]({0})",
						url.Action("ArchiveById", "ViewPost", new { Id = int.Parse(match.Groups[1].Value) }, optionalProtocolForAbsoluteUrls)
					);
				},
				RegexOptions.Multiline
			);
		}

		public static IHtmlString RenderPostAsPlainTextWithSearchTermsHighlighted(
			this HtmlHelper helper,
			Post post,
			string searchTerms,
			int maxLengthOfEncodedContent,
			ICache cache)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (cache == null)
				throw new ArgumentNullException("cache");
			if (string.IsNullOrWhiteSpace(searchTerms))
				throw new ArgumentException("Null/blank searchTerms specified");
			if (maxLengthOfEncodedContent <= 0)
				throw new ArgumentOutOfRangeException("maxLength");

			// Always need to HtmlEncode the content (to ensure that "<" and other characters are escaped, even though we know there is no markup returned
			// from GetPostAsPlainText)
			var htmlEncodedPlainTextContent = HttpUtility.HtmlEncode(GetPostAsPlainText(post, cache));

			// Try to determine which segments of the content should be highlighted as matched search terms
			// - Note: This method uses a simple StringComparison.InvariantCultureIgnoreCase with a whole-word-only matching requirement so it's possible
			//   that not all of the search terms will be present in the posts indicated by FullTextIndexer results, depending upon the StringNormaliser
			//   used by the indexer (eg. if a plurality-handling normaliser is used then it may match "properties" to a post the contains the word
			//   "property" but the IdentifySearchTerms method does not have this additional logic and so will not be able to mark "property" for
			//  highlighting as it isn't one of the search terms!
			var segmentMatchRegions = IdentifySearchTerms(
				htmlEncodedPlainTextContent,
				new NonNullOrEmptyStringList(searchTerms.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).Distinct()),
				maxLengthOfEncodedContent,
				new SearchTermBestMatchComparer()
			);
			if (!segmentMatchRegions.Any())
			{
				// If none of the search terms matched then just return the original content
				return (IHtmlString)MvcHtmlString.Create(
					TruncateAsNecessary(htmlEncodedPlainTextContent, maxLengthOfEncodedContent, "...")
				);
			}

			// Otherwise jump to the start of the matched content - ordinarily this will mean jumping to the lowest value in the bestMatch array and
			// then taking the next maxLengthOfEncodedContent number of characters, but if this would push us past the end of the string then we're
			// better off starting from the end and taking maxLengthOfEncodedContent BACK from there
			int numberOfCharactersToTrimFromTheStart, segmentToDisplayLength;
			if (maxLengthOfEncodedContent > htmlEncodedPlainTextContent.Length)
			{
				// This is an easy case; we're able to show the entire content, so no characters need be removed from the start of it
				numberOfCharactersToTrimFromTheStart = 0;
				segmentToDisplayLength = htmlEncodedPlainTextContent.Length;
			}
			else
			{
				// The next case is optimistic; if every match term can be fitted into the first maxLengthOfEncodedContent number of characters
				// then display the string starting at the beginning
				var firstCharacterIndex = segmentMatchRegions.Min(s => s.Start);
				int lastCharacterIndex = 0;
				foreach (var segment in segmentMatchRegions)
				{
					if ((segment.Start + segment.Length) > lastCharacterIndex)
						lastCharacterIndex = segment.Start + segment.Length;
				}
				if (lastCharacterIndex < maxLengthOfEncodedContent)
				{
					numberOfCharactersToTrimFromTheStart = 0;
					segmentToDisplayLength = maxLengthOfEncodedContent;
				}
				else
				{
					// Otherwise we're going to have to take a segment out of the content
					if ((firstCharacterIndex + maxLengthOfEncodedContent) > htmlEncodedPlainTextContent.Length)
						numberOfCharactersToTrimFromTheStart = htmlEncodedPlainTextContent.Length - maxLengthOfEncodedContent;
					else
						numberOfCharactersToTrimFromTheStart = firstCharacterIndex;
					segmentToDisplayLength = maxLengthOfEncodedContent;
				}
			}

			// - Any overlapping regions are combined, then the regions are adjusted by subtracting numberOfCharactersToTrimFromTheStart
			//   from the each Start value to account for the portion of the content that will be displayed
			var adjustedSegmentMatchRegions =
				CombineOverlappingSegments(segmentMatchRegions)
				.Select(s => new StringSegment(s.Start - numberOfCharactersToTrimFromTheStart, s.Length)); ;
			// - They are then processed in reverse order so that matches later in the string can not affect indexes of matches earlier
			//   in the string
			var htmlEncodedPlainTextContentSegment = htmlEncodedPlainTextContent.Substring(numberOfCharactersToTrimFromTheStart, segmentToDisplayLength);
			foreach (var segment in adjustedSegmentMatchRegions.OrderByDescending(s => s.Start))
			{
				htmlEncodedPlainTextContentSegment = string.Format(
					"{0}<strong>{1}</strong>{2}",
					htmlEncodedPlainTextContentSegment.Substring(0, segment.Start),
					htmlEncodedPlainTextContentSegment.Substring(segment.Start, segment.Length),
					htmlEncodedPlainTextContentSegment.Substring(segment.Start + segment.Length)
				);
			}
			if (numberOfCharactersToTrimFromTheStart > 0)
				htmlEncodedPlainTextContentSegment = "... " + htmlEncodedPlainTextContentSegment;
			if ((numberOfCharactersToTrimFromTheStart + maxLengthOfEncodedContent) < htmlEncodedPlainTextContent.Length)
				htmlEncodedPlainTextContentSegment += "... ";
			return (IHtmlString)MvcHtmlString.Create(htmlEncodedPlainTextContentSegment);
		}

		/// <summary>
		/// This will try to identify segments of a content string to highlight that correspond to specified search terms (the terms will be matched without
		/// case sensitivity but they must be surrounded by whitespace or punctuation - unless they are right at the start or end of the content - in order
		/// to ensure that only whole words are matched). The segments returned will all be displayable within a segment of the content that is no longer
		/// than the maxLength value - this may result in compromises being made and not all terms may have highlight segments returned (feasibly an
		/// empty list will be returned if it was not possible to locate and/or match any of the terms in the content given the maxLength constraint).
		/// </summary>
		private static NonNullImmutableList<StringSegment> IdentifySearchTerms(
			string content,
			NonNullOrEmptyStringList searchTerms,
			int maxLength,
			IComparer<NonNullImmutableList<StringSegment>> bestMatchDeterminer)
		{
			if (content == null)
				throw new ArgumentNullException("content");
			if (searchTerms == null)
				throw new ArgumentNullException("searchTerms");
			if (!searchTerms.Any())
				throw new ArgumentException("No searchTerms specified");
			if (bestMatchDeterminer == null)
				throw new ArgumentNullException("bestMatchDeterminer");

			if (content.Trim() == "")
				return new NonNullImmutableList<StringSegment>();

			// Try to find starting points in the content for each of the search terms, only search terms that appear at the start of end of string or
			// that are surrounded by whitespace or puncutation are considered in order to match whole words only)
			// - Passing null to Split means break on any whitespace
			var searchTermMatches = new List<Tuple<string, List<int>>>();
			foreach (var searchTerm in searchTerms)
			{
				var start = -1;
				while (true)
				{
					start = content.IndexOf(searchTerm, start + 1, StringComparison.InvariantCultureIgnoreCase);
					if (start == -1)
						break;

					if (start > 0)
					{
						var previousCharacter = content[start - 1];
						if (!char.IsWhiteSpace(previousCharacter) && !char.IsPunctuation(previousCharacter))
							continue;
					}
					if ((start + searchTerm.Length) < content.Length)
					{
						var followingCharacter = content[start + searchTerm.Length];
						if (!char.IsWhiteSpace(followingCharacter) && !char.IsPunctuation(followingCharacter))
							continue;
					}

					var dataToAddTo = searchTermMatches.FirstOrDefault(e => e.Item1 == searchTerm);
					if (dataToAddTo == null)
					{
						searchTermMatches.Add(Tuple.Create(searchTerm, new List<int>()));
						dataToAddTo = searchTermMatches.Last();
					}
					dataToAddTo.Item2.Add(start);
					start++;
				}
			}
			if (searchTermMatches.Count == 0)
				return new NonNullImmutableList<StringSegment>();

			// Generate all permutations of search term match index values, incorporating -1 values so that we include permutations which don't match all
			// of the words - eg. if searching for "find this" and we located "find" and "this" at positions [ 7, 102 ] and [ 13, 37, 200 ] then come up
			// with the complete set of permutations:
			//   [  -1, -1 ], [  -1, 13 ], [  -1, 37 ], [  -1, 200 ],
			//   [   7, -1 ], [   7, 13 ], [   7, 37 ], [   7, 200 ],
			//   [ 102, -1 ], [ 102, 13 ], [ 102, 37 ], [ 102, 200 ]
			// We'll have to use ToArray on the LINQ results to ensure that they are executed now and not lazily, otherwise they will receive the wrong
			// value for index!
			var allPermutations = (new[] { -1 }).Concat(searchTermMatches[0].Item2).Select(v => new[] { v }).ToArray();
			for (var index = 1; index < searchTermMatches.Count; index++)
			{
				allPermutations = allPermutations
					.SelectMany(a11 => (new[] { -1 }).Concat(searchTermMatches[index].Item2).Select(v => a11.Concat(new[] { v }).ToArray()))
					.ToArray();
			}

			// Now determine which of these permutations are valid taking into account the maxLengthOfEncodedContent and keep track of the best one
			var bestMatch = new NonNullImmutableList<StringSegment>();
			foreach (var permutation in allPermutations)
			{
				// Ignore the case where all values are -1 as this means that no match is made, we want the case where at least one word is matched
				if (permutation.All(v => v == -1))
					continue;

				// Determine what section of the original content would have to be displayed in order to show all matched words in the current permutation
				var min = int.MaxValue;
				var max = 0;
				for (var index = 0; index < permutation.Length; index++)
				{
					var start = permutation[index];
					if (start == -1)
						continue;

					if (start < min)
						min = start;
					var end = start + searchTermMatches[index].Item1.Length;
					if (end > max)
						max = end;
				}

				// If this section length exceeds maxLengthOfEncodedContent then it's not valid, so skip over it
				if ((max - min) > maxLength)
					continue;

				// Otherwise, record it is as bestMatch if the current bestMatch is null or this is an improvement
				// - A permutation is a better match than an existing match if it has less -1 value, in other words if it manages to match more words
				var permutationAsStringSegmentsTemp = new List<StringSegment>();
				for (var index = 0; index < permutation.Length; index++)
				{
					if (permutation[index] != -1)
						permutationAsStringSegmentsTemp.Add(new StringSegment(permutation[index], searchTermMatches[index].Item1.Length));
				}
				var permutationAsStringSegments = permutationAsStringSegmentsTemp.ToNonNullImmutableList();
				if (bestMatchDeterminer.Compare(bestMatch, permutationAsStringSegments) == 1)
					bestMatch = permutationAsStringSegments;
			}

			// Note: bestMatch may remain as an empty list if we didn't manage to find any matches (this should only be the case if maxLengthOfEncodedContent
			// is very short and the only matched words all exceed it in length)
			return bestMatch;
		}

		private class SearchTermBestMatchComparer : IComparer<NonNullImmutableList<StringSegment>>
		{
			public int Compare(NonNullImmutableList<StringSegment> x, NonNullImmutableList<StringSegment> y)
			{
				// If both terms are null then there's nothing in it, if one is null then prefer the non-null value
				if ((x == null) && (y == null))
					return 0;
				else if (y == null)
					return -1;
				else if (x == null)
					return 1;

				// If x matches more terms then prefer x
				if (x.Count != y.Count)
					return (x.Count > y.Count) ? -1 : 1;

				// If they match the same number of terms then prefer the one that matches the longest term(s)
				var xSortedByLength = x.Select(v => v.Length).OrderByDescending(v => v).ToArray();
				var ySortedByLength = y.Select(v => v.Length).OrderByDescending(v => v).ToArray();
				for (var index = 0; index < x.Count; index++)
				{
					var xN = xSortedByLength[index];
					var yN = ySortedByLength[index];
					if (xN > yN)
						return -1;
					else if (yN > xN)
						return 1;
				}

				// If they match the same number of terms and the matched terms are all of the same lengths then prefer one that starts earlier on in the string
				return x.Min(v => v.Start).CompareTo(y.Min(v => v.Start));
			}
		}

		private static IEnumerable<StringSegment> CombineOverlappingSegments(IEnumerable<StringSegment> segments)
		{
			if (segments == null)
				throw new ArgumentNullException("segments");
			if (segments.Any(s => s == null))
				throw new ArgumentException("Null reference encountered in segments set");

			var segmentsCombined = new List<StringSegment>();
			foreach (var segment in segments.OrderBy(s => s.Start))
			{
				var previousSegment = segmentsCombined.Any() ? segmentsCombined.Last() : null;
				if ((previousSegment == null) || (segment.Start > (previousSegment.Start + previousSegment.Length)))
				{
					segmentsCombined.Add(segment);
					continue;
				}
				segmentsCombined.Remove(previousSegment);
				segmentsCombined.Add(new StringSegment(
					previousSegment.Start,
					segment.Length + previousSegment.Length - (segment.Start - previousSegment.Start))
				);
			}
			return segmentsCombined;
		}

		private class StringSegment
		{
			public StringSegment(int start, int length)
			{
				if (start < 0)
					throw new ArgumentOutOfRangeException("start");
				if (length <= 0)
					throw new ArgumentOutOfRangeException("length");

				Start = start;
				Length = length;
			}
			public int Start { get; private set; }
			public int Length { get; private set; }
		}

		private static string GetPostAsPlainText(Post post, ICache cache)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (cache == null)
				throw new ArgumentNullException("cache");

			var cacheKey = "PostHelper-RenderPostAsPlainText-" + post.Id;
			var doc = new HtmlDocument();
			doc.LoadHtml(
				MarkdownHelper.TransformIntoHtml(post.MarkdownContent)
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
			if (content.StartsWith(post.Title.Trim()))
				content = content.Substring(post.Title.Trim().Length).Trim();

			cache[cacheKey] = new CachablePostContent(content, post.LastModified);
			return content;
		}

		private static string TruncateAsNecessary(string content, int maxLength, string truncationIndicator)
		{
			if (content == null)
				throw new ArgumentNullException("content");
			if (maxLength <= 0)
				throw new ArgumentOutOfRangeException("maxLength");
			if (truncationIndicator == null)
				throw new ArgumentNullException("truncationIndicator");

			if (content.Length > maxLength)
				return content.Substring(0, maxLength) + truncationIndicator;

			return content;
		}

		private const string FirstLineTitleIdentifierPattern = @"^(#+)\s*(.*?)(\n|\r)";

		/// <summary>
		/// If the first line of the content is a header (defined by a number of "#" characters) then wrap this in a link to the post it is part of
		/// </summary>
		private static string ReplaceFirstLineHashHeaderWithPostLink(string content, int postId)
		{
			if (string.IsNullOrEmpty(content))
				return content;

			return Regex.Replace(
				content.Trim(),
				FirstLineTitleIdentifierPattern,
				delegate(Match match)
				{
					// 2012-11-10: Also add an anchor so that we can create hash tags to link to Posts - this would only cause an issue if
					// the same Post was rendered multiple times on a page (then there'd be an id clash), but this should never happen
					return String.Format(
						"{0}<a id=\"Post{2}\"></a>[{1}](Post{2})",
						match.Groups[1].Value,
						match.Groups[2].Value,
						postId
					);
				},
				RegexOptions.Singleline
			);
		}

		/// <summary>
		/// If the first line of the content is a header (defined by a number of "#" characters) then remove this first line
		/// </summary>
		private static string RemoveTitle(string content)
		{
			if (string.IsNullOrEmpty(content))
				return content;

			return Regex.Replace(
				content.Trim(),
				FirstLineTitleIdentifierPattern,
				"",
				RegexOptions.Singleline
			);
		}

		[Serializable]
		private class CachablePostContent
		{
			public CachablePostContent(string renderableContent, DateTime lastModified)
			{
				if (renderableContent == null)
					throw new ArgumentNullException("renderableContent");

				RenderableContent = renderableContent;
				LastModified = lastModified;
			}

			/// <summary>
			/// This will never be null
			/// </summary>
			public string RenderableContent { get; private set; }
			public DateTime LastModified { get; private set; }
		}
	}
}
