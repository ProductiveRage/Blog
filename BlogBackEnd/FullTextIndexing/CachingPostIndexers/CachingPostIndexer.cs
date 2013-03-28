using System;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
	public class CachingPostIndexer :  IPostIndexer
	{
		private IPostIndexCache _cache;
		private IPostIndexer _postIndexer;
		public CachingPostIndexer(IPostIndexer postIndexer, IPostIndexCache cache)
		{
			if (cache == null)
				throw new ArgumentNullException("cache");
			if (postIndexer == null)
				throw new ArgumentNullException("postIndexer");

			_cache = cache;
			_postIndexer = postIndexer;
		}

		/// <summary>
		/// This will never return null, it will throw an exception for null input.
		/// </summary>
		public PostIndexContent GenerateIndexContent(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException("posts");

			return GetData(posts).Index;
		}

		private CachedPostIndexContent GetData(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException("posts");

			var cachedData = _cache.TryToRetrieve();
			if ((cachedData != null) && cachedData.IsValidForPostsData(posts))
				return cachedData;

			var postIndexData = _postIndexer.GenerateIndexContent(posts);
			var liveData = new CachedPostIndexContent(
				postIndexData,
				posts
			);
			_cache.Store(liveData);
			return liveData;
		}
	}
}
