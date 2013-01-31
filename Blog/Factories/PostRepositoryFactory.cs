using System;
using System.IO;
using System.Web;
using Blog.Models;
using BlogBackEnd.Caching;

namespace Blog.Factories
{
	public class PostRepositoryFactory
	{
		private HttpContextBase _httpContext;
		public PostRepositoryFactory(HttpContextBase httpContext)
		{
			if (httpContext == null)
				throw new ArgumentNullException("httpContext");

			_httpContext = httpContext;
		}

		public IPostRepository Get()
		{
			var postsFolder = new DirectoryInfo(_httpContext.Server.MapPath("~/App_Data/Posts"));
			var longTermCache = new ASPNetCacheCache(
				_httpContext.Cache,
				TimeSpan.FromDays(1)
			);
			var shortTermCache = new ASPNetCacheCache(
				_httpContext.Cache,
				TimeSpan.FromSeconds(5)
			);
			return new AppDataTextPostRepository(
				new CachedSingleFolderPostRetriever(
					new LastModifiedCachedSingleFolderPostRetriever(
						new SingleFolderPostRetriever(postsFolder),
						postsFolder,
						longTermCache
					),
					postsFolder,
					shortTermCache
				)
			);
		}
	}
}
