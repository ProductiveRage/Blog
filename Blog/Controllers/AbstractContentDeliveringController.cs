using System;
using System.IO.Compression;
using System.Linq;
using System.Web.Mvc;

namespace Blog.Controllers
{
	public abstract class AbstractContentDeliveringController : AbstractErrorLoggingController
    {
		/// <summary>
		/// Try to get the If-Modified-Since HttpHeader value - if not present or not valid (ie. not interpretable as a date) then null will be returned
		/// </summary>
		protected DateTime? TryToGetIfModifiedSinceDateFromRequest()
		{
			var lastModifiedDateRaw = Request.Headers["If-Modified-Since"];
			if (lastModifiedDateRaw == null)
				return null;

			DateTime lastModifiedDate;
			if (DateTime.TryParse(lastModifiedDateRaw, out lastModifiedDate))
				return lastModifiedDate;

			return null;
		}

		/// <summary>
		/// Mark the response as being cacheable and implement content-encoding requests such that gzip is used if supported by requester
		/// </summary>
		protected void SetResponseCacheHeadersForSuccess(DateTime lastModifiedDateOfLiveData)
		{
			// Mark the response as cacheable
			// - Specify "Vary" "Content-Encoding" header to ensure that if cached by proxies that different versions are stored for different encodings
			//   (eg. gzip'd vs non-gzip'd)
			Response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
			Response.Cache.SetLastModified(lastModifiedDateOfLiveData);
			Response.AppendHeader("Vary", "Content-Encoding");

			// Handle requested content-encoding method
			var encodingsAccepted = (Request.Headers["Accept-Encoding"] ?? "").Split(',').Select(e => e.Trim().ToLower()).ToArray();
			if (encodingsAccepted.Contains("gzip"))
			{
				Response.AppendHeader("Content-encoding", "gzip");
				Response.Filter = new GZipStream(Response.Filter, CompressionMode.Compress);
			}
			else if (encodingsAccepted.Contains("deflate"))
			{
				Response.AppendHeader("Content-encoding", "deflate");
				Response.Filter = new DeflateStream(Response.Filter, CompressionMode.Compress);
			}
		}
	}
}
