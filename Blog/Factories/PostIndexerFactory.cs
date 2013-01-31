using System;
using System.IO;
using System.Web;
using BlogBackEnd.FullTextIndexing;
using BlogBackEnd.FullTextIndexing.CachingPostIndexers;

namespace Blog.Factories
{
	public class PostIndexerFactory
	{
		private HttpContextBase _httpContext;
		public PostIndexerFactory(HttpContextBase httpContext)
		{
			if (httpContext == null)
				throw new ArgumentNullException("httpContext");

			_httpContext = httpContext;
		}

		public IPostIndexer Get()
		{
			// Return a PostIndexer wrapped in ASP.Net Cache and File-based caching layers (cached data will be expired if there 
			var fileCachedPostIndex = new CachingPostIndexer(
				new PostIndexer(),
				new FileBasedPostIndexCache(
					new FileInfo(_httpContext.Server.MapPath("~/App_Data/SearchIndex.dat"))
				)
			);
			return new CachingPostIndexer(
				fileCachedPostIndex,
				new ASPNetCachePostIndexCache(HttpContext.Current.Cache, TimeSpan.FromHours(24))
			);
		}
	}
}
