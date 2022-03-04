using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.FullTextIndexing;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Blog.Helpers
{
    public static class PostHelper
	{
        public static HtmlString RenderPostTitleSummary(this IHtmlHelper helper, PostWithRelatedPostStubs post)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (post == null)
                throw new ArgumentNullException(nameof(post));

            var content = new StringBuilder();
            AppendPostDate(content, post.Posted);
            content.AppendFormat("<h2><a href=\"/{0}\">{1}</a></h2>", HttpUtility.HtmlAttributeEncode(post.Slug), HttpUtility.HtmlEncode(post.Title));
            content.Append(GetTagLinksContent(helper, post.Tags));
            return new HtmlString(content.ToString());
        }

        public static async Task<HtmlString> RenderPost(
            this IHtmlHelper helper,
            PostWithRelatedPostStubs post,
            Post previousPostIfAny,
            Post nextPostIfAny,
            IRetrievePostSlugs postSlugRetriever,
            ICache cache)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (post == null)
                throw new ArgumentNullException(nameof(post));
            if (postSlugRetriever == null)
                throw new ArgumentNullException(nameof(postSlugRetriever));
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            var cacheKey = string.Format(
                "PostHelper-RenderPost-{0}-{1}-{2}",
                post.Id,
                (previousPostIfAny == null) ? 0 : previousPostIfAny.Id,
                (nextPostIfAny == null) ? 0 : nextPostIfAny.Id
            );
            var cachedData = cache[cacheKey];
            if (cachedData != null)
            {
                if ((cachedData is CachablePostContent cachedPostContent) && (cachedPostContent.LastModified >= post.LastModified))
                    return new HtmlString(cachedPostContent.RenderableContent);
                cache.Remove(cacheKey);
            }

            var content = await GetRenderableContent(helper, post, previousPostIfAny, nextPostIfAny, includeTitle: true, includePostedDate: true, includeTags: true, postSlugRetriever);
            cache[cacheKey] = new CachablePostContent(content, post.LastModified);
            return new HtmlString(content);
        }

        public static async Task<string> RenderPostForRSS(this IHtmlHelper helper, PostWithRelatedPostStubs post, string scheme, HostString host, IRetrievePostSlugs postSlugRetriever, ICache cache)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (post == null)
                throw new ArgumentNullException(nameof(post));
			if (string.IsNullOrWhiteSpace(scheme))
				throw new ArgumentException("Null/blank/whitespace-only scheme");
			if (!host.HasValue)
				throw new ArgumentException("No host value");
			if (postSlugRetriever == null)
                throw new ArgumentNullException(nameof(postSlugRetriever));
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            var cacheKey = "PostHelper-RenderPostForRSS-" + post.Id;
            var cachedData = cache[cacheKey];
            if (cachedData != null)
            {
                if ((cachedData is CachablePostContent cachedPostContent) && (cachedPostContent.LastModified >= post.LastModified))
                    return cachedPostContent.RenderableContent;
                cache.Remove(cacheKey);
            }

            var content = await GetRenderableContent(helper, post, previousPostIfAny: null, nextPostIfAny: null, includeTitle: false, includePostedDate: false, includeTags: false, postSlugRetriever);
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            MakeUrlsAbsolute(doc, "a", "href", scheme, host);
            MakeUrlsAbsolute(doc, "img", "src", scheme, host);
            content = doc.DocumentNode.OuterHtml;

            cache[cacheKey] = new CachablePostContent(content, post.LastModified);
            return content;
        }

        private static void AppendPostDate(StringBuilder content, DateTime postedAt) =>
            content.AppendFormat("<p class=\"PostDate\">{0}</p>", postedAt.ToString("d MMMM yyyy"));

        private static async Task<string> GetRenderableContent(
            IHtmlHelper helper,
            PostWithRelatedPostStubs post,
            Post previousPostIfAny,
            Post nextPostIfAny,
            bool includeTitle,
            bool includePostedDate,
            bool includeTags,
            IRetrievePostSlugs postSlugRetriever)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (post == null)
                throw new ArgumentNullException(nameof(post));
            if (postSlugRetriever == null)
                throw new ArgumentNullException(nameof(postSlugRetriever));

            var markdownContent = includeTitle ? (await ReplaceFirstLineHashHeaderWithPostLink(post, postSlugRetriever)) : RemoveTitle(post.MarkdownContent);
            markdownContent = await HandlePostLinks(helper, markdownContent, postSlugRetriever);
            markdownContent = HandleTagLinks(helper, markdownContent);

            var content = new StringBuilder();
            if (includePostedDate)
                AppendPostDate(content, post.Posted);
            content.Append(
                MarkdownTransformations.ToHtml(markdownContent, post.Slug)
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
                        helper.RenderedActionLink(previousPostIfAny.Title, "ArchiveBySlug", "ViewPost", new { previousPostIfAny.Slug }, new { @class = "Previous" })
                    );
                    content.Append("</div>");
                }
                if (nextPostIfAny != null)
                {
                    content.Append("<div class=\"Next\">");
                    content.Append("<h3>Next:</h3>");
                    content.Append(
						helper.RenderedActionLink(nextPostIfAny.Title, "ArchiveBySlug", "ViewPost", new { nextPostIfAny.Slug }, new { @class = "Next" })
					);
                    content.Append("</div>");
                }
                content.Append("</div>");
            }
            if (post.RelatedPosts.Any())
            {
                content.Append("<div class=\"Related\">");
                content.Append("<h3>You may also be interested in:</h3>");
                content.Append("<ul>");
                foreach (var relatedPost in post.RelatedPosts)
                {
                    content.AppendFormat(
                        "<li>{0}</li>",
                        helper.RenderedActionLink(relatedPost.Title, "ArchiveBySlug", "ViewPost", new { relatedPost.Slug }, null)
                    );
                }
                content.Append("</ul>");
                content.Append("</div>");
            }
			else if (post.AutoSuggestedRelatedPosts.Any())
			{
				// Only display the auto-suggested related posts if there are no manually-picked related posts
				content.Append("<div class=\"Related\">");
				content.AppendFormat(
					"<h3>You may also be interested in (see {0} for information about how these are generated):</h3>",
					helper.RenderedActionLink("here", "ArchiveBySlug", "ViewPost", new { Slug = "automating-suggested-related-posts-links-for-my-blog-posts" }, null)
				);
				content.Append("<ul>");
				foreach (var relatedPost in post.AutoSuggestedRelatedPosts)
				{
					content.AppendFormat(
						"<li>{0}</li>",
						helper.RenderedActionLink(relatedPost.Title, "ArchiveBySlug", "ViewPost", new { relatedPost.Slug }, null)
					);
				}
				content.Append("</ul>");
				content.Append("</div>");
			}
			if (includeTags)
                content.Append(GetTagLinksContent(helper, post.Tags));
            return content.ToString();
        }

        private static string GetTagLinksContent(IHtmlHelper helper, NonNullImmutableList<TagSummary> tags)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (tags == null)
                throw new ArgumentNullException(nameof(tags));

            var tagsToDisplay = tags.Where(t => t.NumberOfPosts > 1); // There's no point rendering a tag unless other Posts have the same tag
            if (!tagsToDisplay.Any())
                return "";

            var content = new StringBuilder();
            content.Append("<div class=\"Tags\">");
            content.Append("<label>Tags:</label>");
            content.Append("<ul>");
            foreach (var tagSummary in tagsToDisplay)
            {
                content.AppendFormat(
                    "<li>{0}</li>",
                    helper.RenderedActionLink(tagSummary.Tag, "ArchiveByTag", "ViewPost", new { tagSummary.Tag }, new { title = tagSummary.NumberOfPosts + " Posts" })
                );
            }
            content.Append("</ul>");
            content.Append("</div>");
            return content.ToString();
        }

		/// <summary>
		/// Update any markdown links where the target is of the form "Post{0}" to replace with the real url (linking to the Post with the specified Id)
		/// </summary>
		private static async Task<string> HandlePostLinks(IHtmlHelper helper, string content, IRetrievePostSlugs postSlugRetriever)
		{
			if (helper == null)
				throw new ArgumentNullException(nameof(helper));
			if (string.IsNullOrEmpty(content))
				return content;
			if (postSlugRetriever == null)
				throw new ArgumentNullException(nameof(postSlugRetriever));

			var rewrittenContent = new StringBuilder();
			var lastIndex = 0;
			foreach (Match match in Regex.Matches(content, @"\[([^\]]*?)\]\(Post(\d+)\)", RegexOptions.Multiline))
			{
				var text = match.Groups[1].Value;
				var id = int.Parse(match.Groups[2].Value);

				rewrittenContent
					.Append(content, lastIndex, match.Index - lastIndex)
					.Append(helper.RenderedActionLink(text, "ArchiveBySlug", "ViewPost", new { Slug = await postSlugRetriever.GetSlug(id) }));

				lastIndex = match.Index + match.Length;
			}
			rewrittenContent.Append(content, lastIndex, content.Length - lastIndex);
			return rewrittenContent.ToString();
		}

		/// <summary>
		/// Update any markdown links where the target is of the form "Tag:{0}" to replace with the real url (link to the specified Tag)
		/// </summary>
		private static string HandleTagLinks(IHtmlHelper helper, string content)
		{
			if (helper == null)
				throw new ArgumentNullException(nameof(helper));
			if (string.IsNullOrEmpty(content))
				return content;

			var rewrittenContent = new StringBuilder();
			var lastIndex = 0;
			foreach (Match match in Regex.Matches(content, @"\[([^\]]*?)\]\(Tag:(.*?)\)", RegexOptions.Multiline))
			{
				var text = match.Groups[1].Value;
				var tag = match.Groups[2].Value;

				rewrittenContent
					.Append(content, lastIndex, match.Index - lastIndex)
					.Append(helper.RenderedActionLink(text, "ArchiveByTag", "ViewPost", new { Tag = tag }));

				lastIndex = match.Index + match.Length;
			}
			rewrittenContent.Append(content, lastIndex, content.Length - lastIndex);
			return rewrittenContent.ToString();
		}

		private static void MakeUrlsAbsolute(HtmlDocument doc, string tagName, string attributeName, string scheme, HostString host)
		{
			if (doc == null)
				throw new ArgumentNullException(nameof(doc));
			if (string.IsNullOrWhiteSpace(tagName))
				throw new ArgumentException("Null/blank tagName specified");
			if (string.IsNullOrWhiteSpace(attributeName))
				throw new ArgumentException("Null/blank attributeName specified");
			if (string.IsNullOrWhiteSpace(scheme))
				throw new ArgumentException("Null/blank scheme specified");
			if (!host.HasValue)
				throw new ArgumentException("No host value");

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
					host.Host,
					(host.Port == 80) ? "" : (":" + host.Port),
					attribute.Value.ToString().TrimStart('/')
				);
			}
		}

		public static HtmlString RenderPostAsPlainTextWithSearchTermsHighlighted(
			Post post,
			NonNullImmutableList<SourceFieldLocation> sourceLocations,
			int maxLength,
			ICache cache)
		{
			if (post == null)
				throw new ArgumentNullException(nameof(post));
			if (cache == null)
				throw new ArgumentNullException(nameof(cache));
			if (sourceLocations == null)
				throw new ArgumentNullException(nameof(sourceLocations));
			if (!sourceLocations.Any())
				throw new ArgumentException("Empty sourceLocations set specified - invalid");
			if (maxLength <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxLength));
			if (cache == null)
				throw new ArgumentNullException(nameof(cache));

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
					var unhighlightedContentToAdd = plainTextPostContent[endIndexOfLastSection..matchToHighlight.Index];
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

			return new HtmlString(highlightedContentBuilder.ToString());
		}

		// Sort the possibilities by weight (in descending order) and then by number of tokens matched (in descending order; the logic being that if a set of fewer
		// tokens have the same weight as a set with more tokens then the tokens in the smaller set must be of greater weight and so more important). If further
		// sorting is required then prefere content closer to the start than the end.
		private class SearchTermBestMatchComparer : IComparer<NonNullImmutableList<SourceFieldLocation>>
		{
			public int Compare(NonNullImmutableList<SourceFieldLocation> x, NonNullImmutableList<SourceFieldLocation> y)
			{
				if (x == null)
					throw new ArgumentNullException(nameof(x));
				if (y == null)
					throw new ArgumentNullException(nameof(y));

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
				throw new ArgumentNullException(nameof(post));
			if (cache == null)
				throw new ArgumentNullException(nameof(cache));

			var cacheKey = "PostHelper-RenderPostAsPlainText-" + post.Id;
            if ((cache[cacheKey] is CachablePostContent cachedData) && (cachedData.LastModified >= post.LastModified))
                return cachedData.RenderableContent;

            var content = post.GetContentAsPlainText();
			cache[cacheKey] = new CachablePostContent(content, post.LastModified);
			return content;
		}

		private const string FirstLineTitleIdentifierPattern = @"^(#+)(\s*)(.*?)(\n|\r)";

		/// <summary>
		/// If the first line of the content is a header (defined by a number of "#" characters) then wrap this in a link to the post it is part of
		/// </summary>
		private static async Task<string> ReplaceFirstLineHashHeaderWithPostLink(Post post, IRetrievePostSlugs postSlugRetriever)
		{
			if (post == null)
				throw new ArgumentNullException(nameof(post));

			var slug = await postSlugRetriever.GetSlug(post.Id);
			return Regex.Replace(
				post.MarkdownContent.Trim(),
				FirstLineTitleIdentifierPattern,
				match =>
				{
					// 2012-11-10: Also add an anchor so that we can create hash tags to link to Posts - this would only cause an issue if
					// the same Post was rendered multiple times on a page (then there'd be an id clash), but this should never happen
					var titleHashSymbols = match.Groups[1].Value;
					var spaceAfterHashSymbols = match.Groups[2].Value;
					var title = match.Groups[3].Value;
					return $"{titleHashSymbols}{spaceAfterHashSymbols}<a href=\"/{slug}\">{HttpUtility.HtmlEncode(title)}</a>";
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
                RenderableContent = renderableContent ?? throw new ArgumentNullException(nameof(renderableContent));
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
