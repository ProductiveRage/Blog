using System;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public interface IPostRepository
	{
		NonNullImmutableList<Post> GetAll();

		/// <summary>
		/// This is case insensitive, it will return an empty list if no Posts have the specified tag, it will raise an exception for a null or blank tag
		/// </summary>
		NonNullImmutableList<Post> GetByTag(string tag);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		NonNullImmutableList<Post> GetByDateRange(DateTime min, DateTime max);

		NonNullImmutableList<Post> GetByIds(ImmutableList<int> ids);

		/// <summary>
		/// This is case sensitive, it will return null if the slug is invalid
		/// </summary>
		PostMatchDetails GetBySlug(string slug);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		NonNullImmutableList<PostStub> GetStubs(DateTime? min, DateTime? max, bool highlightsOnly);

		NonNullImmutableList<PostStub> GetMostRecentStubs(int count);

		DateTime? GetMinPostDate();
		DateTime? GetMaxPostDate();
	}
}
