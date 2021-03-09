using System;
using System.Collections.Concurrent;

namespace BlogBackEnd.Caching
{
    public sealed class ConcurrentDictionaryCache : ICache
	{
		private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

		private readonly TimeSpan _cacheDuration;
		public ConcurrentDictionaryCache(TimeSpan cacheDuration)
		{
			if (cacheDuration.Ticks <= 0)
				throw new ArgumentOutOfRangeException("cacheDuration", "cacheDuration must be > 0");

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

				return (_cache.TryGetValue(key.Trim(), out var valueFromCache) && !valueFromCache.HasExpired)
					? valueFromCache?.Value
					: null;
			}
			set
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");
				
				key = key.Trim();

				if (_cache.TryGetValue(key.Trim(), out var valueFromCache))
				{
					if (!valueFromCache.HasExpired)
						return;

					_cache.TryRemove(key, out var _);
				}

				_cache.TryAdd(key, new CacheEntry(key, value, DateTime.Now.Add(_cacheDuration)));
			}
		}

		/// <summary>
		/// This will do nothing if the key is not present in the cache
		/// </summary>
		public void Remove(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentException("Null/empty cacheKey specified");

			_cache.TryRemove(key.Trim(), out var _);
		}

		private sealed class CacheEntry
		{
			private readonly DateTime _expiresAtUtc;
			public CacheEntry(string key, object value, DateTime expiresAtUtc)
			{
				Key = key;
				Value = value;
				_expiresAtUtc = expiresAtUtc;
			}
			
			public string Key { get; }
			public object Value { get; }
			public bool HasExpired => DateTime.UtcNow >= _expiresAtUtc;
		}
	}
}
