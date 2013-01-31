using System;
using System.Web.Caching;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
	public class ASPNetCachePostIndexCache : IPostIndexCache
	{
		private const string CacheKey = "ASPNetCachePostIndexCache";

		private Cache _cache;
		private TimeSpan _cacheDuration;
		public ASPNetCachePostIndexCache(Cache cache, TimeSpan cacheDuration)
		{
			if (cache == null)
				throw new ArgumentNullException("cache");
			if (cacheDuration.Ticks <= 0)
				throw new ArgumentOutOfRangeException("cacheDuration", "cacheDuration must be > 0");

			_cache = cache;
			_cacheDuration = cacheDuration;
		}

		/// <summary>
		/// This will return null if unable to deliver the data
		/// </summary>
		public CachedPostIndexContent TryToRetrieve()
		{
			return _cache[CacheKey] as CachedPostIndexContent;
		}

		/// <summary>
		/// If an entry already exists in the cache, it will be overwritten. It will throw an exception for a null data reference.
		/// </summary>
		public void Store(CachedPostIndexContent data)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			// Ensure that any existing entry is removed before adding a new one (for cases where existing content contains expired data)
			_cache.Remove(CacheKey);
			_cache.Add(CacheKey, data, null, DateTime.Now.Add(_cacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
		}
	}
}
