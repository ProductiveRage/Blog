using System.Threading.Tasks;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public interface ISingleFolderPostRetriever
	{
		/// <summary>
		/// This will never return null nor contain any null entries
		/// </summary>
		Task<NonNullImmutableList<Post>> Get();
	}
}
