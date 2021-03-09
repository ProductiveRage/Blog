using System;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
    public sealed class CachingPostIndexer :  IPostIndexer
	{
		private readonly IPostIndexCache _cache;
		private readonly IPostIndexer _postIndexer;
		public CachingPostIndexer(IPostIndexer postIndexer, IPostIndexCache cache)
		{
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
			_postIndexer = postIndexer ?? throw new ArgumentNullException(nameof(postIndexer));
		}

		/// <summary>
		/// This will never return null, it will throw an exception for null input.
		/// </summary>
		public PostIndexContent GenerateIndexContent(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException(nameof(posts));

			return GetData(posts).Index;
		}

		private CachedPostIndexContent GetData(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException(nameof(posts));

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
