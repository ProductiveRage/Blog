using System;
using BlogBackEnd.Models;

namespace Blog.Models
{
	[Serializable]
	public class SearchResult
	{
		public SearchResult(Post post, float weight)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (weight <= 0)
				throw new ArgumentOutOfRangeException("weight");

			Post = post;
			Weight = weight;
		}

		/// <summary>
		/// This will be null
		/// </summary>
		public Post Post { get; private set; }

		/// <summary>
		/// This will always be greater than zero
		/// </summary>
		public float Weight { get; private set; }
	}
}
