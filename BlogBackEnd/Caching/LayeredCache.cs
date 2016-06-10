using System;
using System.Collections.Generic;
using System.Linq;

namespace BlogBackEnd.Caching
{
	public sealed class LayeredCache : ICache
	{
		private readonly IEnumerable<ICache> _caches;
		public LayeredCache(params ICache[] caches)
		{
			if (caches == null)
				throw new ArgumentNullException("caches");

			_caches = caches.ToList().AsReadOnly();
			if (_caches.Any(cache => cache == null))
				throw new ArgumentException("Null reference encountered in cache set");
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

				var missCaches = new List<ICache>();
				foreach (var cache in _caches)
				{
					var cachedValue = cache[key];
					if (cachedValue == null)
					{
						missCaches.Add(cache);
						continue;
					}
					foreach (var cacheToBackFill in missCaches)
						cacheToBackFill[key] = cachedValue;
					return cachedValue;
				}
				return null;
			}
			set
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new ArgumentException("Null/blank key specified");

				foreach (var cache in _caches)
					cache[key] = value;
			}
		}

		/// <summary>
		/// This will do nothing if the key is not present in the cache. It will throw an exception for an null or empty key.
		/// </summary>
		public void Remove(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("Null/blank key specified");
			
			foreach (var cache in _caches)
				cache.Remove(key);
		}

		public void RemoveAll()
		{
			foreach (var cache in _caches)
				cache.RemoveAll();
		}
	}
}
