using System;
using System.Threading.Tasks;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public class CachedSingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private readonly ISingleFolderPostRetriever _singleFolderPostRetriever;
		private readonly ICache _cache;
		private readonly string _cacheKey;
		public CachedSingleFolderPostRetriever(ISingleFolderPostRetriever singleFolderPostRetriever, string folderName, ICache cache)
		{
			if (singleFolderPostRetriever == null)
				throw new ArgumentNullException("singleFolderPostRetriever");
			if (string.IsNullOrWhiteSpace(folderName))
				throw new ArgumentException("Null/blank/whitespace-only folderName specified");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_singleFolderPostRetriever = singleFolderPostRetriever;
			_cache = cache;
			_cacheKey = "CachedSingleFolderPostRetriever-" + folderName;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public async Task<NonNullImmutableList<Post>> Get()
		{
			var cachedData = _cache[_cacheKey] as NonNullImmutableList<Post>;
			if (cachedData != null)
				return cachedData;

			var liveData = await _singleFolderPostRetriever.Get();
			_cache[_cacheKey] = liveData;
			return liveData;
		}
	}
}
