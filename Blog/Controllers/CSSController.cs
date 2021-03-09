using System;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using CSSMinifier.Caching;
using CSSMinifier.FileLoaders;
using CSSMinifier.FileLoaders.Factories;
using CSSMinifier.FileLoaders.LastModifiedDateRetrievers;
using CSSMinifier.PathMapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Blog.Controllers
{
    public class CSSController : Controller
	{
		private readonly IFileProvider _contentRootFileProvider;
		public CSSController(IFileProvider contentRootFileProvider)
		{
			if (contentRootFileProvider == null)
				throw new ArgumentNullException("contentRootFileProvider");

			_contentRootFileProvider = contentRootFileProvider;
		}

		public IActionResult Process()
		{
			var relativePathMapper = new ContentRootPathMapper(_contentRootFileProvider);
			var relativePath = Request.Path;
			var fullPath = relativePathMapper.MapPath(relativePath);
			var file = new FileInfo(fullPath);
			if (!file.Exists)
			{
				Response.StatusCode = 404;
				return Content("File not found: " + relativePath, "text/css");
			}

			try
			{
				return Process(
					relativePath,
					relativePathMapper,
					new NonExpiringASPNetCacheCache(),
					TryToGetIfModifiedSinceDateFromRequest()
				);
			}
			catch (Exception e)
			{
				Response.StatusCode = 500;
				return Content("Error: " + e.Message);
			}
		}

		/// <summary>
		/// This will combine a stylesheet with all of its imports (and any imports within those, and within those, etc..) and minify the resulting content for cases only
		/// where all files are in the same folder and no relative or absolute paths are specified in the import declarations. It incorporates caching of the minified
		/// content and implements 304 responses for cases where the request came with an If-Modified-Since header indicating that current content already exists on the
		/// client. The last-modified-date for the content is determined by retrieving the most recent LastWriteTime for any file in the folder - although this may lead
		/// to some false-positives if unrelated files are updated, it does mean that if any file that IS part of the combined stylesheet is updated then the content
		/// will be identified as stale and re-generated. The cached content will likewise be invalidated and updated if any files in the folder have changed since the
		/// date recorded for the cached data. GZip and Deflate compression of the response are supported where specified in Accept-Encoding request headers.
		/// </summary>
		private ActionResult Process(
			string relativePath,
			IRelativePathMapper relativePathMapper,
			ICacheThingsWithModifiedDates<TextFileContents> memoryCache,
			DateTime? lastModifiedDateFromRequest)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");
			if (memoryCache == null)
				throw new ArgumentNullException("memoryCache");
			if (relativePathMapper == null)
				throw new ArgumentNullException("relativePathMapper");

			// Using the SingleFolderLastModifiedDateRetriever means that we can determine whether cached content (either in the ASP.Net cache or in the browser cache)
			// is up to date without having to perform the complete import flattening process. It may lead to some unnecessary work if an unrelated file in the folder
			// is updated but for the most common cases it should be an efficient approach.
			var lastModifiedDateRetriever = new SingleFolderLastModifiedDateRetriever(relativePathMapper, new[] { "css", "less" });
			var lastModifiedDate = lastModifiedDateRetriever.GetLastModifiedDate(relativePath);
			if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, lastModifiedDate))
			{
				Response.StatusCode = 304;
				return Content("Not changed since last request", "text/css");
			}

			var cssLoader = (new EnhancedNonCachedLessCssLoaderFactory(
				relativePathMapper,
				SourceMappingMarkerInjectionOptions.Inject,
				ErrorBehaviourOptions.LogAndRaiseException,
				new CSSMinifier.Logging.NullLogger()
			)).Get();

			// Ignore any errors from the DiskCachingTextFileLoader - if the file contents become invalid then allow them to be deleted and rebuilt instead of blowing
			// up. The EnhancedNonCachedLessCssLoaderFactory should raise exceptionns since it will indicate invalid source content, which should be flagged up.
			var modifiedDateCachingStyleLoader = new CachingTextFileLoader(
				new DiskCachingTextFileLoader(
					cssLoader,
					relativePathRequested => new FileInfo(relativePathMapper.MapPath(relativePathRequested) + ".cache"),
					lastModifiedDateRetriever,
					DiskCachingTextFileLoader.InvalidContentBehaviourOptions.Delete,
					ErrorBehaviourOptions.LogAndContinue,
					new CSSMinifier.Logging.NullLogger()
				),
				lastModifiedDateRetriever,
				memoryCache
			);
			
			var content = modifiedDateCachingStyleLoader.Load(relativePath);
			if (content == null)
				throw new Exception("Received null response from Css Loader - this should not happen");
			if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, lastModifiedDate))
			{
				Response.StatusCode = 304;
				return Content("Not changed since last request", "text/css");
			}
			SetResponseCacheHeadersForSuccess(content.LastModified);
			return Content(content.Content, "text/css");
		}

		/// <summary>
		/// Try to get the If-Modified-Since HttpHeader value - if not present or not valid (ie. not interpretable as a date) then null will be returned
		/// </summary>
		private DateTime? TryToGetIfModifiedSinceDateFromRequest()
		{
			var lastModifiedDateRaw = Request.Headers["If-Modified-Since"].FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
			if (lastModifiedDateRaw == null)
				return null;

            if (DateTime.TryParse(lastModifiedDateRaw, out DateTime lastModifiedDate))
                return lastModifiedDate;

            return null;
		}

		/// <summary>
		/// Dates from HTTP If-Modified-Since headers are only precise to whole seconds while files' LastWriteTime are granular to milliseconds, so when
		/// comparing them a small grace period is required
		/// </summary>
		private static bool AreDatesApproximatelyEqual(DateTime d1, DateTime d2)
		{
			return Math.Abs(d1.Subtract(d2).TotalSeconds) < 1;
		}

		/// <summary>
		/// Mark the response as being cacheable and implement content-encoding requests such that gzip is used if supported by requester
		/// </summary>
		private void SetResponseCacheHeadersForSuccess(DateTime lastModifiedDateOfLiveData)
		{
			// Mark the response as cacheable
			// - Specify "Vary" "Content-Encoding" header to ensure that if cached by proxiesthat different versions are stored for different encodings (eg. gzip'd vs non-gzip'd)

			Response.Headers.Add(HeaderNames.CacheControl, "Public");
			Response.Headers.Add(HeaderNames.Vary, "Content-Encoding");

			Response.Headers.Add(HeaderNames.LastModified, lastModifiedDateOfLiveData.ToString("yyyy-MM-dd HH:mm:ss.fff"));

			// As suggested at https://andrewlock.net/adding-cache-control-headers-to-static-files-in-asp-net-core/#cache-busting-for-file-changes
			Response.Headers.Add(HeaderNames.ETag, Convert.ToString(lastModifiedDateOfLiveData.ToFileTime(), 16));
		}

		/// <summary>
		/// This will throw an exception for null or empty input, it will never return null
		/// </summary>
		private class ContentRootPathMapper : IRelativePathMapper
		{
			private readonly IFileProvider _contentRootFileProvider;
			public ContentRootPathMapper(IFileProvider contentRootFileProvider)
			{
				if (contentRootFileProvider == null)
					throw new ArgumentNullException("contentRootFileProvider");

				_contentRootFileProvider = contentRootFileProvider;
			}

			public string MapPath(string relativePath)
			{
				if (string.IsNullOrWhiteSpace(relativePath))
					throw new ArgumentException("Null/blank relativePath specified");

				return _contentRootFileProvider.GetFileInfo(relativePath).PhysicalPath;
			}
		}

		private class NonExpiringASPNetCacheCache : ICacheThingsWithModifiedDates<TextFileContents>
		{
			private static readonly MemoryCache _cache = new MemoryCache("CssCache");

			/// <summary>
			/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
			/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
			/// </summary>
			public TextFileContents this[string cacheKey]
			{
				get
				{
					if (string.IsNullOrWhiteSpace(cacheKey))
						throw new ArgumentException("Null/blank cacheKeys specified");

					var cachedData = _cache.Get(cacheKey);
					if (cachedData == null)
						return null;

					var cachedTextFileContentsData = cachedData as TextFileContents;
					if (cachedTextFileContentsData != null)
						return cachedTextFileContentsData;
				
					// If something's inserted invalid data into the cache then remove it, since whatever's call this getter will probably want to insert its own data
					// after it does the work to generate it (and the Add method won't overwrite data already in the cache)
					Remove(cacheKey);
					return null;
				}
			}

			/// <summary>
			/// The caching mechanism (eg. cache duration) is determine by the ICache implementation. This will throw an exception for null or blank cacheKey or null value.
			/// </summary>
			public void Add(string cacheKey, TextFileContents value)
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentException("Null/blank cacheKeys specified");
				if (value == null)
					throw new ArgumentNullException("value");

				// Since the CSSController will push cached data in with a LastModifiedDate and then replace those cache items (with a Remove followed by Add) then we can
				// use DateTime.MaxValue for AbsoluteExpiration and effectively disable time-based expiration
				_cache.Set(cacheKey, value, DateTimeOffset.MaxValue);
			}

			/// <summary>
			/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
			/// </summary>
			public void Remove(string cacheKey)
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentException("Null/blank cacheKeys specified");

				_cache.Remove(cacheKey);
			}
		}
	}
}