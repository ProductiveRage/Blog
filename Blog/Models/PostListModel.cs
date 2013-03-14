using System;
using System.Collections.Generic;
using System.Linq;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class PostListModel : ICommonSiteConfigModel, ISideBarContentModel
	{
		public PostListModel(
			string title,
			IEnumerable<Post> posts,
			IEnumerable<PostStub> recent,
			IEnumerable<PostStub> highlights,
			IEnumerable<ArchiveMonthLink> archiveLinks,
			bool isSinglePostView,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
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
			if (postContentCache == null)
				throw new ArgumentNullException("cache");

			Title = title.Trim();
			Posts = new NonNullImmutableList<Post>(posts.Where(p => p != null));
			MostRecent = new NonNullImmutableList<PostStub>(recent.Where(p => p != null).OrderByDescending(p => p.Posted));
			Highlights = new NonNullImmutableList<PostStub>(highlights.Where(p => p != null));
			ArchiveLinks = new NonNullImmutableList<ArchiveMonthLink>(archiveLinks.Where(l => l != null).OrderByDescending(a => new DateTime(a.Year, a.Month, 1)));
			IsSinglePostView = isSinglePostView;
			OptionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			OptionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			PostContentCache = postContentCache;
		}

		/// <summary>
		/// This will never be null or blank
		/// </summary>
		public string Title { get; private set; }

		/// <summary>
		/// This will never return null
		/// </summary>
		public NonNullImmutableList<Post> Posts { get; private set; }

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
		public string OptionalGoogleAnalyticsId { get; private set; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		public string OptionalDisqusShortName { get; private set; }

		/// <summary>
		/// This will never be null
		/// </summary>
		public ICache PostContentCache { get; private set; }
	}
}
