using System;
using System.IO;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using Common.Lists;

namespace Blog.Models
{
	public class CachedSingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private ISingleFolderPostRetriever _singleFolderPostRetriever;
		private ICache _cache;
		private string _cacheKey;
		public CachedSingleFolderPostRetriever(ISingleFolderPostRetriever singleFolderPostRetriever, DirectoryInfo folder, ICache cache)
		{
			if (singleFolderPostRetriever == null)
				throw new ArgumentNullException("singleFolderPostRetriever");
			if (folder == null)
				throw new ArgumentNullException("folder");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_singleFolderPostRetriever = singleFolderPostRetriever;
			_cache = cache;
			_cacheKey = "CachedSingleFolderPostRetriever-" + folder.FullName;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public NonNullImmutableList<Post> Get()
		{
			var cachedData = _cache[_cacheKey] as NonNullImmutableList<Post>;
			if (cachedData != null)
				return cachedData;

			var liveData = _singleFolderPostRetriever.Get();
			_cache[_cacheKey] = liveData;
			return liveData;
		}
	}
}
