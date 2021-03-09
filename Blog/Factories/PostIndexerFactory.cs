using System;
using System.IO;
using BlogBackEnd.FullTextIndexing;
using BlogBackEnd.FullTextIndexing.CachingPostIndexers;
using Microsoft.Extensions.FileProviders;

namespace Blog.Factories
{
    public sealed class PostIndexerFactory
	{
		private readonly IFileProvider _fileProvider;
		public PostIndexerFactory(IFileProvider fileProvider)
		{
            _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
		}

		public IPostIndexer Get()
		{
			// Return a PostIndexer wrapped in an in-memory cache and File-based caching layers
			var fileCachedPostIndex = new CachingPostIndexer(
				new PostIndexer(),
				new FileBasedPostIndexCache(
					new FileInfo(_fileProvider.GetFileInfo("/App_Data/SearchIndex.dat").PhysicalPath)
				)
			);
			return new CachingPostIndexer(
				fileCachedPostIndex,
				new ConcurrentDictionaryPostIndexCache(TimeSpan.FromHours(24))
			);
		}
	}
}