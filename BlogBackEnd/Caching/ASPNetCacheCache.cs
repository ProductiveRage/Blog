using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Caching;

namespace BlogBackEnd.Caching
{
	public class ASPNetCacheCache : ICache
	{
		private Cache _cache;
		private TimeSpan _cacheDuration;
		public ASPNetCacheCache(Cache cache, TimeSpan cacheDuration)
		{
			if (cache == null)
				throw new ArgumentNullException("cache");
			if (cacheDuration.Ticks <= 0)
				throw new ArgumentOutOfRangeException("cacheDuration", "cacheDuration must be > 0");

			_cache = cache;
			_cacheDuration = cacheDuration;
		}

		/// <summary>
		/// The getter will return null if there is no cached data matching the specified key. The setter will only write the data if the key is not already present in the data
		/// (so that the cache can implement its own expiration handling and callers can make push requests to the data without having to worry about checking whether it's already
		/// there or not - if a caller really wants to overwrite any present data, the Remove method may be called first). Both getter and setter will throw an exception for a null
		/// or empty key. The setter will throw an exception if a null value is specified.
		/// </summary>
		public object this[string key]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");

				return _cache[key.Trim()];
			}
			set
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");

				key = key.Trim();
				if (_cache[key] == null)
					_cache.Add(key, value, null, DateTime.Now.Add(_cacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.NotRemovable, null);
			}
		}

		/// <summary>
		/// This will do nothing if the key is not present in the cache
		/// </summary>
		public void Remove(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Null/empty cacheKey specified");

			_cache.Remove(key.Trim());
		}

		public void RemoveAll()
		{
			var allKeys = new List<string>();
			foreach (DictionaryEntry entry in _cache)
				allKeys.Add((string)entry.Key);
			foreach (var key in allKeys)
				_cache.Remove(key);
		}
	}
}
