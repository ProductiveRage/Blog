using System;
using System.Threading.Tasks;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public sealed class CachedSingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private readonly ISingleFolderPostRetriever _singleFolderPostRetriever;
		private readonly ICache _cache;
		private readonly string _cacheKey;
		public CachedSingleFolderPostRetriever(ISingleFolderPostRetriever singleFolderPostRetriever, string folderName, ICache cache)
		{
            if (string.IsNullOrWhiteSpace(folderName))
				throw new ArgumentException("Null/blank/whitespace-only folderName specified");
            _singleFolderPostRetriever = singleFolderPostRetriever ?? throw new ArgumentNullException(nameof(singleFolderPostRetriever));
			_cache = cache ?? throw new ArgumentNullException(nameof(cache));
			_cacheKey = "CachedSingleFolderPostRetriever-" + folderName;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public async Task<NonNullImmutableList<Post>> Get()
		{
            if (_cache[_cacheKey] is NonNullImmutableList<Post> cachedData)
                return cachedData;

            var liveData = await _singleFolderPostRetriever.Get();
			_cache[_cacheKey] = liveData;
			return liveData;
		}
	}
}
