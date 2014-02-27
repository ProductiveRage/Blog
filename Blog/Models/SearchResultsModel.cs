using System;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class SearchResultsModel : ICommonSiteConfigModel, ISideBarContentModel
	{
		public SearchResultsModel(
			string searchTerm,
			NonNullImmutableList<SearchResult> results,
			NonNullImmutableList<PostStub> recent,
			NonNullImmutableList<PostStub> highlights,
			NonNullImmutableList<ArchiveMonthLink> archiveLinks,
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			ICache postContentCache)
		{
			if (searchTerm == null)
				throw new ArgumentNullException("searchTerm");
			if (results == null)
				throw new ArgumentNullException("results");
			if (recent == null)
				throw new ArgumentNullException("recent");
			if (highlights == null)
				throw new ArgumentNullException("highlights");
			if (archiveLinks == null)
				throw new ArgumentNullException("archiveLinks");
			if (postContentCache == null)
				throw new ArgumentNullException("cache");

			SearchTerm = searchTerm.Trim();
			Results = results.Sort((x, y) => -x.Weight.CompareTo(y.Weight));
			MostRecent = recent.Sort((x, y) => -x.Posted.CompareTo(y.Posted));
			Highlights = highlights;
			ArchiveLinks = archiveLinks.Sort((x, y) => -(new DateTime(x.Year, x.Month, 1)).CompareTo(new DateTime(y.Year, y.Month, 1)));
			OptionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			OptionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			PostContentCache = postContentCache;
		}

		/// <summary>
		/// This will never be null but it may be blank (this is the actual search term entered by the user)
		/// </summary>
		public string SearchTerm { get; private set; }

		/// <summary>
		/// This will never be null but it may be an empty list (the results will be ordered by descending weight)
		/// </summary>
		public NonNullImmutableList<SearchResult> Results { get; private set; }

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
		/// This will never be null
		/// </summary>
		public ICache PostContentCache { get; private set; }

		/// <summary>
		/// If this is not a page that search engines should store in their index, but that links should be followed from, so that the pages that
		/// SHOULD be stored can be located, then this should be set to true. This helps prevent the spiders from confusing the content of the
		/// individual posts with content of the pages that include those posts (such as the home page or monthly archives)
		/// </summary>
		public bool MarkPageAsFollowNoIndex { get { return true; } }
	}
}
