using System;

namespace Blog.Controllers
{
    public sealed class SiteConfiguration
	{
		public SiteConfiguration(
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
			string optionalTwitterUserName,
			string optionalTwitterImage,
			int maximumNumberOfPostsToPublishInRssFeed)
		{
			if (maximumNumberOfPostsToPublishInRssFeed <= 0)
				throw new ArgumentOutOfRangeException("maximumNumberOfPostsToPublishInRssFeed", "must be greater than zero");

			OptionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			OptionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			OptionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			OptionalTwitterUserName = string.IsNullOrWhiteSpace(optionalTwitterUserName) ? null : optionalTwitterUserName.Trim();
			OptionalTwitterImage = string.IsNullOrWhiteSpace(optionalTwitterImage) ? null : optionalTwitterImage.Trim();
			MaximumNumberOfPostsToPublishInRssFeed = maximumNumberOfPostsToPublishInRssFeed;
		}

		public string OptionalCanonicalLinkBase { get; }
		public string OptionalGoogleAnalyticsId { get; }
		public string OptionalDisqusShortName { get; }
		public string OptionalTwitterUserName { get; }
		public string OptionalTwitterImage { get; }
		public int MaximumNumberOfPostsToPublishInRssFeed { get; }

		public SiteConfiguration RemoveGoogleAnalyticsId()
		{
			return new SiteConfiguration(OptionalCanonicalLinkBase, optionalGoogleAnalyticsId: null, OptionalDisqusShortName, OptionalTwitterUserName, OptionalTwitterImage, MaximumNumberOfPostsToPublishInRssFeed);
		}
	}
}