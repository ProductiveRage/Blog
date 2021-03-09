using System;
using System.IO;
using Blog.Models;
using BlogBackEnd.Caching;
using Microsoft.AspNetCore.Hosting;

namespace Blog.Factories
{
    public sealed class PostRepositoryFactory
	{
		private static readonly ICache _longTermCache = new ConcurrentDictionaryCache(TimeSpan.FromDays(1));
		private static readonly ICache _shortTermCache = new ConcurrentDictionaryCache(TimeSpan.FromSeconds(5));

		private readonly IWebHostEnvironment _hostingEnvironment;
		public PostRepositoryFactory(IWebHostEnvironment hostingEnvironment)
		{
            _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
		}

		public IPostRepository Get()
		{
			const string postsFolderPath = "App_Data/Posts";
			var postsFolder = _hostingEnvironment.ContentRootFileProvider.GetDirectoryContents(postsFolderPath);
			var postsFolderFullPath = _hostingEnvironment.ContentRootFileProvider.GetFileInfo(postsFolderPath).PhysicalPath;
			return new AppDataTextPostRepository(
				new CachedSingleFolderPostRetriever(
					new LastModifiedCachedSingleFolderPostRetriever(
						new SingleFolderPostRetriever(postsFolder),
						new DirectoryInfo(postsFolderFullPath),
						_longTermCache
					),
					_hostingEnvironment.ContentRootPath,
					_shortTermCache
				)
			);
		}
	}
}
