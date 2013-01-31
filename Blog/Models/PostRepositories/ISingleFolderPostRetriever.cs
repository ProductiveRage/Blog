using BlogBackEnd.Models;
using Common.Lists;

namespace Blog.Models
{
	public interface ISingleFolderPostRetriever
	{
		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		NonNullImmutableList<Post> Get();
	}
}
