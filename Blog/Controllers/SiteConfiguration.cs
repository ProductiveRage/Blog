using System;
using Microsoft.AspNetCore.Http;

namespace Blog.Controllers
{
    public sealed class SiteConfiguration
	{
		private readonly string _optionalGoogleAnalyticsId;
		public SiteConfiguration(
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
			string optionalTwitterUserName,
			string optionalTwitterImage,
			int maximumNumberOfPostsToPublishInRssFeed)
		{
			if (maximumNumberOfPostsToPublishInRssFeed <= 0)
				throw new ArgumentOutOfRangeException(nameof(maximumNumberOfPostsToPublishInRssFeed), "must be greater than zero");

			OptionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			_optionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			OptionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			OptionalTwitterUserName = string.IsNullOrWhiteSpace(optionalTwitterUserName) ? null : optionalTwitterUserName.Trim();
			OptionalTwitterImage = string.IsNullOrWhiteSpace(optionalTwitterImage) ? null : optionalTwitterImage.Trim();
			MaximumNumberOfPostsToPublishInRssFeed = maximumNumberOfPostsToPublishInRssFeed;
		}

		public string OptionalCanonicalLinkBase { get; }
		public string OptionalDisqusShortName { get; }
		public string OptionalTwitterUserName { get; }
		public string OptionalTwitterImage { get; }
		public int MaximumNumberOfPostsToPublishInRssFeed { get; }

		public string GetGoogleAnalyticsIdIfAny(HttpRequest request) => IsLocalHost(request ?? throw new ArgumentNullException(nameof(request))) ? null : _optionalGoogleAnalyticsId;

		private static bool IsLocalHost(HttpRequest request)
		{
#if !DEBUG
				return false; // Return false so that the real analytics username is inserted into the content when in release mode, for publishing to GitHub Pages
#else
			return "localhost".Equals((request ?? throw new ArgumentNullException(nameof(request))).Host.Host, StringComparison.OrdinalIgnoreCase);
#endif
		}
	}
}