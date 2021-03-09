using System;
using BlogBackEnd.Caching;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
    public sealed class ConcurrentDictionaryPostIndexCache : IPostIndexCache
	{
		private const string CacheKey = "ASPNetCachePostIndexCache";

		private readonly ConcurrentDictionaryCache _cache;
		public ConcurrentDictionaryPostIndexCache(TimeSpan cacheDuration)
		{
			if (cacheDuration.Ticks <= 0)
				throw new ArgumentOutOfRangeException(nameof(cacheDuration), "cacheDuration must be > 0");

			_cache = new ConcurrentDictionaryCache(cacheDuration);
		}

		/// <summary>
		/// This will return null if unable to deliver the data
		/// </summary>
		public CachedPostIndexContent TryToRetrieve() => _cache[CacheKey] as CachedPostIndexContent;

		/// <summary>
		/// If an entry already exists in the cache, it will be overwritten. It will throw an exception for a null data reference.
		/// </summary>
		public void Store(CachedPostIndexContent data)
		{
            // Ensure that any existing entry is removed before adding a new one (for cases where existing content contains expired data)
            _cache.Remove(CacheKey);
			_cache[CacheKey] = data ?? throw new ArgumentNullException(nameof(data));
		}
	}
}