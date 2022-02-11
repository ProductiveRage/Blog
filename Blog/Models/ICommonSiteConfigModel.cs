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
		/// This may be null but it will never be empty if non-null (if there is a non-null OptionalTwitterCardDetails reference then
		/// the value may match the Description value in there but - as of Feb 2022 - it seems like Google / LightHouse are keen on
		/// seeing a meta description tag even if there's already a Twitter card description)
		/// </summary>
		string OptionalMetaDescription { get; }

		/// <summary>
		/// This may be null if no Twitter meta data should be displayed
		/// </summary>
		TwitterCardDetails OptionalTwitterCardDetails { get; }
	}
}
