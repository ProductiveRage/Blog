namespace Blog.Models
{
	public interface ICommonSiteConfigModel
	{
		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		string OptionalCanonicalLinkBase { get; }

		/// <summary>
		/// This may be null but it will never be empty if non-null
		/// </summary>
		string OptionalGoogleAnalyticsId { get; }
	}
}
