namespace Blog.Models
{
	public interface ICommonSiteConfigModel
	{
		/// <summary>
		/// If this is not a page that search engines should store in their index, but that links should be followed from, so that the pages that
		/// SHOULD be stored can be located, then this should be set to true. This helps prevent the spiders from confusing the content of the
		/// individual posts with content of the pages that include those posts (such as the home page or monthly archives)
		/// </summary>
		bool MarkPageAsFollowNoIndex { get; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		string OptionalCanonicalLinkBase { get; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		string OptionalGoogleAnalyticsId { get; }

		/// <summary>
		/// This may be null if no Twitter meta data should be displayed
		/// </summary>
		TwitterCardDetails OptionalTwitterCardDetails { get; }
	}
}
