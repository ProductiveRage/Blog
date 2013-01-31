using System;
using BlogBackEnd.Models;
using Common.Lists;

namespace Blog.Models
{
	public interface IPostRepository
	{
		NonNullImmutableList<Post> GetAll();

		/// <summary>
		/// This is case insensitive
		/// </summary>
		NonNullImmutableList<Post> Get(string tag);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		NonNullImmutableList<Post> Get(DateTime min, DateTime max);

		NonNullImmutableList<Post> Get(ImmutableList<int> ids);

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		NonNullImmutableList<PostStub> GetStubs(DateTime? min, DateTime? max, bool highlightsOnly);

		NonNullImmutableList<PostStub> GetMostRecentStubs(int count);

		DateTime? GetMinPostDate();
		DateTime? GetMaxPostDate();
	}
}
