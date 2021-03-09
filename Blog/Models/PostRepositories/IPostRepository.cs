using System;
using System.Threading.Tasks;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public interface IPostRepository
	{
		Task<NonNullImmutableList<Post>> GetAll();

		/// <summary>
		/// This is case insensitive, it will return an empty list if no Posts have the specified tag, it will raise an exception for a null or blank tag
		/// </summary>
		Task<NonNullImmutableList<Post>> GetByTag(string tag);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		Task<NonNullImmutableList<Post>> GetByDateRange(DateTime min, DateTime max);

		Task<NonNullImmutableList<Post>> GetByIds(ImmutableList<int> ids);

		/// <summary>
		/// This is case sensitive, it will return null if the slug is invalid
		/// </summary>
		Task<PostMatchDetails> GetBySlug(string slug);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		Task<NonNullImmutableList<PostStub>> GetStubs(DateTime? min, DateTime? max, bool highlightsOnly);

		Task<NonNullImmutableList<PostStub>> GetMostRecentStubs(int count);

		Task<DateTime?> GetMinPostDate();
		Task<DateTime?> GetMaxPostDate();
	}
}
