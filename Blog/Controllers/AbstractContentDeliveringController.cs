using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Blog.Controllers
{
    public abstract class AbstractContentDeliveringController : Controller
    {
		/// <summary>
		/// Try to get the If-Modified-Since HttpHeader value - if not present or not valid (ie. not interpretable as a date) then null will be returned
		/// </summary>
		protected DateTime? TryToGetIfModifiedSinceDateFromRequest()
		{
			var lastModifiedDateRaw = Request.Headers["If-Modified-Since"].FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
			if (lastModifiedDateRaw == null)
				return null;

            if (DateTime.TryParse(lastModifiedDateRaw, out DateTime lastModifiedDate))
                return lastModifiedDate;

            return null;
		}

		/// <summary>
		/// Mark the response as being cacheable
		/// </summary>
		protected void SetResponseCacheHeadersForSuccess(DateTime lastModifiedDateOfLiveData)
		{
			// Mark the response as cacheable
			// - Specify "Vary" "Content-Encoding" header to ensure that if cached by proxies that different versions are stored for different encodings (eg. gzip'd vs non-gzip'd)

			Response.Headers.Add(HeaderNames.CacheControl, "Public");
			Response.Headers.Add(HeaderNames.Vary, "Content-Encoding");

			Response.Headers.Add(HeaderNames.LastModified, lastModifiedDateOfLiveData.ToString("yyyy-MM-dd HH:mm:ss.fff"));

			// As suggested at https://andrewlock.net/adding-cache-control-headers-to-static-files-in-asp-net-core/#cache-busting-for-file-changes
			Response.Headers.Add(HeaderNames.ETag, Convert.ToString(lastModifiedDateOfLiveData.ToFileTime(), 16)); 
		}
	}
}
