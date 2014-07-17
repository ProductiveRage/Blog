using System;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class PostListModel : ICommonSiteConfigModel, ISideBarContentModel
	{
		public PostListModel(
			string title,
			NonNullImmutableList<PostWithRelatedPostStubs> posts,
			NonNullImmutableList<PostStub> recent,
			NonNullImmutableList<PostStub> highlights,
			NonNullImmutableList<ArchiveMonthLink> archiveLinks,
			bool isSinglePostView,
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
			IRetrievePostSlugs postSlugRetriever,
			ICache postContentCache)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new ArgumentException("Null/blank title specified");
			if (posts == null)
				throw new ArgumentNullException("posts");
			if (recent == null)
				throw new ArgumentNullException("recent");
			if (highlights == null)
				throw new ArgumentNullException("highlights");
			if (archiveLinks == null)
				throw new ArgumentNullException("archiveLinks");
			if (postSlugRetriever == null)
				throw new ArgumentNullException("postSlugRetriever");
			if (postContentCache == null)
				throw new ArgumentNullException("cache");

			Title = title.Trim();
			Posts = posts;
			MostRecent = recent.Sort((x, y) => -x.Posted.CompareTo(y.Posted));
			Highlights = highlights;
			ArchiveLinks = archiveLinks.Sort((x, y) => -(new DateTime(x.Year, x.Month, 1)).CompareTo(new DateTime(y.Year, y.Month, 1)));
			IsSinglePostView = isSinglePostView;
			OptionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			OptionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			OptionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			PostSlugRetriever = postSlugRetriever;
			PostContentCache = postContentCache;
		}

		/// <summary>
		/// This will never be null or blank
		/// </summary>
		public string Title { get; private set; }

		/// <summary>
		/// This will never return null
		/// </summary>
		public NonNullImmutableList<PostWithRelatedPostStubs> Posts { get; private set; }

		/// <summary>
		/// This may be used to determine - for example - whether comments should be enabled in the view (if they are only to be enabled when viewing
		/// a particular post)
		/// </summary>
		public bool IsSinglePostView { get; private set; }

		/// <summary>
		/// This will never return null (the results will be ordered in descending date order)
		/// </summary>
		public NonNullImmutableList<PostStub> MostRecent { get; private set; }

		/// <summary>
		/// This will never return null (no ordering is guaranteed)
		/// </summary>
		public NonNullImmutableList<PostStub> Highlights { get; private set; }

		/// <summary>
		/// This will never return null (the results will be ordered in descending date order, by month)
		/// </summary>
		public NonNullImmutableList<ArchiveMonthLink> ArchiveLinks { get; private set; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		public string OptionalCanonicalLinkBase { get; private set; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		public string OptionalGoogleAnalyticsId { get; private set; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		public string OptionalDisqusShortName { get; private set; }

		/// <summary>
		/// This will never be null
		/// </summary>
		public IRetrievePostSlugs PostSlugRetriever { get; private set; }
		
		/// <summary>
		/// This will never be null
		/// </summary>
		public ICache PostContentCache { get; private set; }

		/// <summary>
		/// If this is not a page that search engines should store in their index, but that links should be followed from, so that the pages that
		/// SHOULD be stored can be located, then this should be set to true. This helps prevent the spiders from confusing the content of the
		/// individual posts with content of the pages that include those posts (such as the home page or monthly archives)
		/// </summary>
		public bool MarkPageAsFollowNoIndex
		{
			get
			{
				// If this is a Single Post View (meaning it is intended to show one particular Post, not just that a search was performed which
				// happened to match on a single Post) then we want search engine to index that as the primary location of that content and to
				// ignore it if it appears on the home page. So return true (indicating "follow, noindex") UNLESS it's a Single Post View.
				return !IsSinglePostView;
			}
		}
	}
}
