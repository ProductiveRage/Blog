using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public interface ISideBarContentModel
	{
		/// <summary>
		/// This will never return null (the results will be ordered in descending date order)
		/// </summary>
		NonNullImmutableList<PostStub> MostRecent { get; }

		/// <summary>
		/// This will never return null (no ordering is guaranteed)
		/// </summary>
		NonNullImmutableList<PostStub> Highlights { get; }

		/// <summary>
		/// This will never return null (the results will be ordered in descending date order, by month)
		/// </summary>
		NonNullImmutableList<ArchiveMonthLink> ArchiveLinks { get; }
	}
}
