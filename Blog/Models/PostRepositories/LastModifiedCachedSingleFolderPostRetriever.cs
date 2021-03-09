using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public class LastModifiedCachedSingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private readonly ISingleFolderPostRetriever _singleFolderPostRetriever;
		private readonly DirectoryInfo _folder;
		private readonly ICache _cache;
		private readonly string _cacheKey;
		public LastModifiedCachedSingleFolderPostRetriever(ISingleFolderPostRetriever singleFolderPostRetriever, DirectoryInfo folder, ICache cache)
		{
			if (singleFolderPostRetriever == null)
				throw new ArgumentNullException("singleFolderPostRetriever");
			if (folder == null)
				throw new ArgumentNullException("folder");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_singleFolderPostRetriever = singleFolderPostRetriever;
			_folder = folder;
			_cache = cache;
			_cacheKey = "LastModifiedCachedSingleFolderPostRetriever-" + folder.FullName;
		}

		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		public async Task<NonNullImmutableList<Post>> Get()
		{
			if (!_folder.Exists)
				return NonNullImmutableList<Post>.Empty;

			if (!_folder.EnumerateFiles().Any())
				return NonNullImmutableList<Post>.Empty;

			var lastModified = _folder.EnumerateFiles().Max(f => f.LastWriteTimeUtc);
			var cachedData = _cache[_cacheKey] as CachedResult;
			if ((cachedData != null) && (cachedData.LastModifiedUtc >= lastModified))
				return cachedData.Posts;

			var liveData = await _singleFolderPostRetriever.Get();
			_cache[_cacheKey] = new CachedResult(liveData, lastModified);
			return liveData;
		}

		[Serializable]
		private class CachedResult
		{
			public CachedResult(NonNullImmutableList<Post> posts, DateTime lastModifiedUtc)
			{
				if (posts == null)
					throw new ArgumentNullException("post");

				Posts = posts;
				LastModifiedUtc = lastModifiedUtc;
			}

			public NonNullImmutableList<Post> Posts { get; private set; }
			public DateTime LastModifiedUtc { get; private set; }
		}
	}
}
