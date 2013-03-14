using System;
using System.IO;
using System.Linq;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class LastModifiedCachedSingleFolderPostRetriever : ISingleFolderPostRetriever
	{
		private ISingleFolderPostRetriever _singleFolderPostRetriever;
		private DirectoryInfo _folder;
		private ICache _cache;
		private string _cacheKey;
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
		public NonNullImmutableList<Post> Get()
		{
			if (!_folder.Exists)
				return new NonNullImmutableList<Post>();

			var files = _folder.GetFiles();
			if (!files.Any())
				return new NonNullImmutableList<Post>();

			var lastModified = files.Max(f => f.LastWriteTime);
			var cachedData = _cache[_cacheKey] as CachedResult;
			if ((cachedData != null) && (cachedData.LastModified >= lastModified))
				return cachedData.Posts;

			var liveData = _singleFolderPostRetriever.Get();
			_cache[_cacheKey] = new CachedResult(liveData, lastModified);
			return liveData;
		}

		[Serializable]
		private class CachedResult
		{
			public CachedResult(NonNullImmutableList<Post> posts, DateTime lastModified)
			{
				if (posts == null)
					throw new ArgumentNullException("post");

				Posts = posts;
				LastModified = lastModified;
			}

			public NonNullImmutableList<Post> Posts { get; private set; }
			public DateTime LastModified { get; private set; }
		}
	}
}
