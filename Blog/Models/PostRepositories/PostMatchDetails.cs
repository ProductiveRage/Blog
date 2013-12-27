using System;
using BlogBackEnd.Models;

namespace Blog.Models
{
	public class PostMatchDetails
	{
		public PostMatchDetails(Post post, PostMatchTypeOptions postMatchType)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (!Enum.IsDefined(typeof(PostMatchTypeOptions), postMatchType))
				throw new ArgumentOutOfRangeException("postMatchType");

			Post = post;
			PostMatchType = postMatchType;
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public Post Post { get; private set; }
		
		public PostMatchTypeOptions PostMatchType { get; private set; }

		public enum PostMatchTypeOptions
		{
			Alias,
			PreciseMatch
		}
	}
}