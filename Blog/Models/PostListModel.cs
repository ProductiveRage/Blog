using System;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public sealed class PostListModel : ICommonSiteConfigModel, ISideBarContentModel
	{
		public PostListModel(
			string title,
			NonNullImmutableList<PostWithRelatedPostStubs> posts,
			Post previousPostIfAny,
			Post nextPostIfAny,
			NonNullImmutableList<PostStub> recent,
			NonNullImmutableList<PostStub> highlights,
			NonNullImmutableList<ArchiveMonthLink> archiveLinks,
			PostListDisplayOptions postListDisplay,
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
			TwitterCardDetails optionalTwitterCardDetails,
			IRetrievePostSlugs postSlugRetriever,
			ICache postContentCache)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new ArgumentException("Null/blank title specified");
            if (recent == null)
				throw new ArgumentNullException(nameof(recent));
            if (archiveLinks == null)
				throw new ArgumentNullException(nameof(archiveLinks));
			if (!Enum.IsDefined(typeof(PostListDisplayOptions), postListDisplay))
				throw new ArgumentOutOfRangeException(nameof(postListDisplay));
            Title = title.Trim();
			Posts = posts ?? throw new ArgumentNullException(nameof(posts));
			PreviousPostIfAny = previousPostIfAny;
			NextPostIfAny = nextPostIfAny;
			MostRecent = recent.Sort((x, y) => -x.Posted.CompareTo(y.Posted));
			Highlights = highlights ?? throw new ArgumentNullException(nameof(highlights));
			ArchiveLinks = archiveLinks.Sort((x, y) => -(new DateTime(x.Year, x.Month, 1)).CompareTo(new DateTime(y.Year, y.Month, 1)));
			PostListDisplay = postListDisplay;
			OptionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			OptionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			OptionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			OptionalTwitterCardDetails = optionalTwitterCardDetails;
			PostSlugRetriever = postSlugRetriever ?? throw new ArgumentNullException(nameof(postSlugRetriever));
			PostContentCache = postContentCache ?? throw new ArgumentNullException(nameof(postContentCache));
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
		/// This will be null if there are no earlier Posts
		/// </summary>
		public Post PreviousPostIfAny { get; private set; }

		/// <summary>
		/// This will be null if there are no later Posts
		/// </summary>
		public Post NextPostIfAny { get; private set; }

		/// <summary>
		/// This may be used to determine - for example - whether comments should be enabled in the view (if they are only to be enabled when viewing
		/// a particular post)
		/// </summary>
		public PostListDisplayOptions PostListDisplay { get; private set; }

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
				return PostListDisplay != PostListDisplayOptions.SinglePost;
			}
		}

		/// <summary>
		/// This may be null if no Twitter meta data should be displayed
		/// </summary>
		public TwitterCardDetails OptionalTwitterCardDetails { get; }
	}
}
