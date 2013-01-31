using BlogBackEnd.Models;
using Common.Lists;

namespace BlogBackEnd.FullTextIndexing
{
	public interface IPostIndexer
	{
		/// <summary>
		/// This will never return null, it will throw an exception for null input.
		/// </summary>
		PostIndexContent GenerateIndexContent(NonNullImmutableList<Post> posts);
	}
}
