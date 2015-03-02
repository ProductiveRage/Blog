using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.FullTextIndexing;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;
using HtmlAgilityPack;

namespace Blog.Helpers.Posts
{
	public static class PostHelper
	{
        public static IHtmlString RenderPostTitleSummary(this HtmlHelper helper, PostWithRelatedPostStubs post)
        {
            if (helper == null)
                throw new ArgumentNullException("helper");
            if (post == null)
                throw new ArgumentNullException("post");

            var content = new StringBuilder();
            content.AppendFormat("<h3 class=\"PostDate\">{0}</h3>", post.Posted.ToString("d MMMM yyyy"));
            content.AppendFormat("<h2><a href=\"{0}\">{1}</a></h2>", HttpUtility.HtmlAttributeEncode(post.Slug), HttpUtility.HtmlEncode(post.Title));
            content.Append(GetTagLinksContent(helper, post.Tags));
            return (IHtmlString)MvcHtmlString.Create(content.ToString());
        }

        public static IHtmlString RenderPost(
            this HtmlHelper helper,
            PostWithRelatedPostStubs post,
            Post previousPostIfAny,
            Post nextPostIfAny,
            IRetrievePostSlugs postSlugRetriever,
            ICache cache)
        {
            if (helper == null)
                throw new ArgumentNullException("helper");
            if (post == null)
                throw new ArgumentNullException("post");
            if (postSlugRetriever == null)
                throw new ArgumentNullException("postSlugRetriever");
            if (cache == null)
                throw new ArgumentNullException("cache");

            var cacheKey = string.Format(
                "PostHelper-RenderPost-{0}-{1}-{2}",
                post.Id,
                (previousPostIfAny == null) ? 0 : previousPostIfAny.Id,
                (nextPostIfAny == null) ? 0 : nextPostIfAny.Id
            );
            var cachedData = cache[cacheKey];
            if (cachedData != null)
            {
                var cachedPostContent = cachedData as CachablePostContent;
                if ((cachedPostContent != null) && (cachedPostContent.LastModified >= post.LastModified))
                    return (IHtmlString)MvcHtmlString.Create(cachedPostContent.RenderableContent);
                cache.Remove(cacheKey);
            }

            var content = GetRenderableContent(helper, post, previousPostIfAny, nextPostIfAny, true, true, true, postSlugRetriever);
            cache[cacheKey] = new CachablePostContent(content, post.LastModified);
            return (IHtmlString)MvcHtmlString.Create(content);
        }

        public static IHtmlString RenderPostForRSS(this HtmlHelper helper, PostWithRelatedPostStubs post, Uri requestHostUrl, IRetrievePostSlugs postSlugRetriever, ICache cache)
        {
            if (helper == null)
                throw new ArgumentNullException("helper");
            if (post == null)
                throw new ArgumentNullException("post");
            if (requestHostUrl == null)
                throw new ArgumentNullException("requestHostUrl");
            if (postSlugRetriever == null)
                throw new ArgumentNullException("postSlugRetriever");
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

            var content = GetRenderableContent(helper, post, null, null, false, false, false, postSlugRetriever);
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            MakeUrlsAbsolute(doc, "a", "href", requestHostUrl.Scheme, requestHostUrl.Host, requestHostUrl.Port);
            MakeUrlsAbsolute(doc, "img", "src", requestHostUrl.Scheme, requestHostUrl.Host, requestHostUrl.Port);
            using (var writer = new StringWriter())
            {
                doc.Save(writer);
                content = writer.ToString();
            }

            cache[cacheKey] = new CachablePostContent(content, post.LastModified);
            return (IHtmlString)MvcHtmlString.Create(content);
        }

        private static string GetRenderableContent(
            HtmlHelper helper,
            PostWithRelatedPostStubs post,
            Post previousPostIfAny,
            Post nextPostIfAny,
            bool includeTitle,
            bool includePostedDate,
            bool includeTags,
            IRetrievePostSlugs postSlugRetriever)
        {
            if (helper == null)
                throw new ArgumentNullException("helper");
            if (post == null)
                throw new ArgumentNullException("post");
            if (postSlugRetriever == null)
                throw new ArgumentNullException("postSlugRetriever");

            var markdownContent = HandlePostLinks(
                helper,
                includeTitle ? ReplaceFirstLineHashHeaderWithPostLink(post.MarkdownContent, post.Id) : RemoveTitle(post.MarkdownContent),
                postSlugRetriever
            );

            var content = new StringBuilder();
            if (includePostedDate)
                content.AppendFormat("<h3 class=\"PostDate\">{0}</h3>", post.Posted.ToString("d MMMM yyyy"));
            content.Append(
                MarkdownHelper.TransformIntoHtml(markdownContent)
            );
            if (includePostedDate)
                content.AppendFormat("<p class=\"PostTime\">Posted at {0}</p>", post.Posted.ToString("HH:mm"));
            if ((previousPostIfAny != null) || (nextPostIfAny != null))
            {
                content.Append("<div class=\"PreviousAndNext\">");
                if (previousPostIfAny != null)
                {
                    content.Append("<div class=\"Previous\">");
                    content.Append("<h3>Last time:</h3>");
                    content.Append(
                        helper.ActionLink(previousPostIfAny.Title, "ArchiveBySlug", "ViewPost", new { Slug = previousPostIfAny.Slug }, new { @class = "Previous" })
                    );
                    content.Append("</div>");
                }
                if (nextPostIfAny != null)
                {
                    content.Append("<div class=\"Next\">");
                    content.Append("<h3>Next:</h3>");
                    content.Append(
                        helper.ActionLink(nextPostIfAny.Title, "ArchiveBySlug", "ViewPost", new { Slug = nextPostIfAny.Slug }, new { @class = "Next" })
                    );
                    content.Append("</div>");
                }
                content.Append("</div>");
            }
            if (post.RelatedPosts.Any())
            {
                content.Append("<div class=\"Related\">");
                content.Append("<h3>You may also be interested in</h3>");
                content.Append("<ul>");
                foreach (var relatedPost in post.RelatedPosts)
                {
                    content.AppendFormat(
                        "<li>{0}</li>",
                        helper.ActionLink(relatedPost.Title, "ArchiveBySlug", "ViewPost", new { Slug = relatedPost.Slug }, null)
                    );
                }
                content.Append("</ul>");
                content.Append("</div>");
            }
            if (includeTags)
                content.Append(GetTagLinksContent(helper, post.Tags));
            return content.ToString();
        }

        private static string GetTagLinksContent(HtmlHelper helper, NonNullImmutableList<TagSummary> tags)
        {
            if (helper == null)
                throw new ArgumentNullException("helper");
            if (tags == null)
                throw new ArgumentNullException("tags");

            var tagsToDisplay = tags.Where(t => t.NumberOfPosts > 1); // There's no point rendering a tag unless other Posts have the same tag
            if (!tagsToDisplay.Any())
                return "";

            var content = new StringBuilder();
            content.Append("<div class=\"Tags\">");
            content.Append("<label>Tags:</label>");
            content.Append("<ul>");
            foreach (var tag in tagsToDisplay.Select(t => t.Tag))
            {
                content.AppendFormat(
                    "<li>{0}</li>",
                    helper.ActionLink(tag, "ArchiveByTag", "ViewPost", new { Tag = tag }, null)
                );
            }
            content.Append("</ul>");
            content.Append("</div>");
            return content.ToString();
        }

        /// <summary>
		/// Update any markdown links where the target is of the form "Post{0}" to replace with the real url
		/// </summary>
		private static string HandlePostLinks(HtmlHelper helper, string content, IRetrievePostSlugs postSlugRetriever)
		{
			if (helper == null)
				throw new ArgumentNullException("helper");
			if (string.IsNullOrEmpty(content))
				return content;
			if (postSlugRetriever == null)
				throw new ArgumentNullException("postSlugRetriever");

			var url = new UrlHelper(helper.ViewContext.RequestContext);
			return Regex.Replace(
				content,
				@"\]\(Post(\d+)\)",
				delegate(Match match)
				{
					var id = int.Parse(match.Groups[1].Value);
					return String.Format(
						"]({0})",
						url.Action("ArchiveBySlug", "ViewPost", new { Slug = postSlugRetriever.GetSlug(id) })
					);
				},
				RegexOptions.Multiline
			);
		}

		private static void MakeUrlsAbsolute(HtmlDocument doc, string tagName, string attributeName, string scheme, string hostName, int port)
		{
			if (doc == null)
				throw new ArgumentNullException("doc");
			if (string.IsNullOrWhiteSpace(tagName))
				throw new ArgumentException("Null/blank tagName specified");
			if (string.IsNullOrWhiteSpace(attributeName))
				throw new ArgumentException("Null/blank attributeName specified");
			if (string.IsNullOrWhiteSpace(scheme))
				throw new ArgumentException("Null/blank scheme specified");
			if (string.IsNullOrWhiteSpace(hostName))
				throw new ArgumentException("Null/blank hostName specified");

			var nodes = doc.DocumentNode.SelectNodes(string.Format("//{0}[@{1}]", tagName, attributeName));
			if (nodes == null)
				return;

			foreach (var node in nodes)
			{
				if (node == null)
					throw new ArgumentException("Null reference encountered in htmlNodes set");

				var attribute = node.Attributes[attributeName];
				if ((attribute == null) || string.IsNullOrWhiteSpace(attribute.Value))
					continue;

				try
				{
					var url = new Uri(attribute.Value, UriKind.RelativeOrAbsolute);
					if (url.IsAbsoluteUri)
						continue;
				}
				catch
				{
					continue; // Ignore any invalid values
				}
				attribute.Value = string.Format(
					"{0}://{1}{2}/{3}",
					scheme,
					hostName,
					(port == 80) ? "" : ":" + port,
					attribute.Value.ToString().TrimStart('/')
				);
			}
		}

		public static IHtmlString RenderPostAsPlainTextWithSearchTermsHighlighted(
			this HtmlHelper helper,
			Post post,
			NonNullImmutableList<IndexData_Extensions_PartialMatches.SourceFieldLocationWithTerm> sourceLocations,
			int maxLength,
			ICache cache)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (cache == null)
				throw new ArgumentNullException("cache");
			if (sourceLocations == null)
				throw new ArgumentNullException("sourceLocations");
			if (!sourceLocations.Any())
				throw new ArgumentException("Empty sourceLocations set specified - invalid");
			if (maxLength <= 0)
				throw new ArgumentOutOfRangeException("maxLength");
			if (cache == null)
				throw new ArgumentNullException("cache");

			// Only consider source locations that come from the Post Content (not Title or Tags), since that is all that can be highlighted (the first
			// Content Retriever is responsible for extracting content from this field so the Source Locations for the Post Content will always have
			// a SourceFieldIndex value of zero)
			var sourceLocationsFromPostContentField = sourceLocations.Where(s => s.SourceFieldIndex == 0);
			var plainTextPostContent = GetPostAsPlainText(post, cache);
			var matchesToHighlight = SearchTermHighlighter.IdentifySearchTermsToHighlight(
				plainTextPostContent,
				maxLength,
				sourceLocationsFromPostContentField.ToNonNullImmutableList(),
				new SearchTermBestMatchComparer()
			);

			int startIndexOfContent;
			if ((plainTextPostContent.Length <= maxLength) || !matchesToHighlight.Any())
				startIndexOfContent = 0;
			else
			{
				if (matchesToHighlight.Max(t => t.Index + t.Length) <= maxLength)
					startIndexOfContent = 0;
				else
				{
					var startIndexOfFirstHighlightedTerm = matchesToHighlight.Min(t => t.Index);
					if (plainTextPostContent.Length - startIndexOfFirstHighlightedTerm <= maxLength)
						startIndexOfContent = plainTextPostContent.Length - maxLength;
					else
						startIndexOfContent = startIndexOfFirstHighlightedTerm;
				}
			}

			// Beginning at this start index, build up a string of content where the highlighted sections are wrapped in "strong" tags (even though the
			// content is plain text, it will need to be html-encoded since plain text content can include characters that should be encoded such as
			// "<" or "&")
			var highlightedContentBuilder = new StringBuilder();
			var endIndexOfLastSection = startIndexOfContent;
			var lengthOfContentIncluded = 0;
			foreach (var matchToHighlight in matchesToHighlight.OrderBy(m => m.Index))
			{
				if (matchToHighlight.Index > endIndexOfLastSection)
				{
					var unhighlightedContentToAdd = plainTextPostContent.Substring(endIndexOfLastSection, matchToHighlight.Index - endIndexOfLastSection);
					highlightedContentBuilder.Append(
						HttpUtility.HtmlEncode(unhighlightedContentToAdd)
					);
					lengthOfContentIncluded += unhighlightedContentToAdd.Length;
				}

				var highlightedContentToAdd = plainTextPostContent.Substring(matchToHighlight.Index, matchToHighlight.Length);
				highlightedContentBuilder.Append("<strong>");
				highlightedContentBuilder.Append(
					HttpUtility.HtmlEncode(highlightedContentToAdd)
				);
				highlightedContentBuilder.Append("</strong>");
				lengthOfContentIncluded += highlightedContentToAdd.Length;

				endIndexOfLastSection = matchToHighlight.Index + matchToHighlight.Length;
			}
			
			// If there's any more content that we can fit into the maxLength constraint after the last highlighted section, then get that too
			if ((lengthOfContentIncluded < maxLength) && (plainTextPostContent.Length > endIndexOfLastSection))
			{
				highlightedContentBuilder.Append(
					HttpUtility.HtmlEncode(
						plainTextPostContent.Substring(
							endIndexOfLastSection,
							Math.Min(
								maxLength - lengthOfContentIncluded,
								plainTextPostContent.Length - endIndexOfLastSection
							)
						)
					)
				);
			}

			// Add leading and/or trailing ellipses to indicate where content has been skipped over
			if (startIndexOfContent > 0)
				highlightedContentBuilder.Insert(0, "..");
			if ((startIndexOfContent + maxLength) < plainTextPostContent.Length)
				highlightedContentBuilder.Append("..");

			return (IHtmlString)MvcHtmlString.Create(highlightedContentBuilder.ToString());
		}

		// Sort the possibilities by weight (in descending order) and then by number of tokens matched (in descending order; the logic being that if a set of fewer
		// tokens have the same weight as a set with more tokens then the tokens in the smaller set must be of greater weight and so more important). If further
		// sorting is required then prefere content closer to the start than the end.
		private class SearchTermBestMatchComparer : IComparer<NonNullImmutableList<SourceFieldLocation>>
		{
			public int Compare(NonNullImmutableList<SourceFieldLocation> x, NonNullImmutableList<SourceFieldLocation> y)
			{
				if (x == null)
					throw new ArgumentNullException("x");
				if (y == null)
					throw new ArgumentNullException("y");

				var combinedWeightComparisonResult = y.Sum(s => s.MatchWeightContribution).CompareTo(x.Sum(s => s.MatchWeightContribution));
				if (combinedWeightComparisonResult != 0)
					return combinedWeightComparisonResult;

				var numberOfTokensComparisonResult = x.Count.CompareTo(y.Count);
				if (numberOfTokensComparisonResult != 0)
					return numberOfTokensComparisonResult;

				return x.Min(s => s.SourceIndex).CompareTo(y.Min(s => s.SourceIndex));
			}
		}

		private static string GetPostAsPlainText(Post post, ICache cache)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (cache == null)
				throw new ArgumentNullException("cache");

			var cacheKey = "PostHelper-RenderPostAsPlainText-" + post.Id;
			var cachedData = cache[cacheKey] as CachablePostContent;
			if ((cachedData != null) && (cachedData.LastModified >= post.LastModified))
				return cachedData.RenderableContent;

			var content = post.GetContentAsPlainText();
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
