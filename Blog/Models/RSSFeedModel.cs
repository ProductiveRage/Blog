using System;
using System.Linq;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class RSSFeedModel
	{
		public RSSFeedModel(NonNullImmutableList<Post> posts, ICache postContentCache)
		{
			if (posts == null)
				throw new ArgumentNullException("posts");
			if (!posts.Any())
				throw new ArgumentException("posts may not be an empty list");
			if (postContentCache == null)
				throw new ArgumentNullException("postContentCache");

			Posts = posts;
			PostContentCache = postContentCache;
		}

		/// <summary>
		/// This will never return null nor empty (if there are no Posts to list in an RSS Feed then a 404 should have been returned).
		///They will be ordered my posted date, descending.
		/// </summary>
		public NonNullImmutableList<Post> Posts { get; private set; }

		/// <summary>
		/// This will never be null
		/// </summary>
		public ICache PostContentCache { get; private set; }
	}
}
