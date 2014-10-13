﻿using System;
using BlogBackEnd.Models;

namespace Blog.Models
{
	public class PostMatchDetails
	{
		public PostMatchDetails(Post post, Post previousPostIfAny, Post nextPostIfAny, PostMatchTypeOptions postMatchType)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (!Enum.IsDefined(typeof(PostMatchTypeOptions), postMatchType))
				throw new ArgumentOutOfRangeException("postMatchType");

			Post = post;
			PreviousPostIfAny = previousPostIfAny;
			NextPostIfAny = nextPostIfAny;
			PostMatchType = postMatchType;
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public Post Post { get; private set; }

		/// <summary>
		/// This will be null if there are no earlier Posts
		/// </summary>
		public Post PreviousPostIfAny { get; private set; }

		/// <summary>
		/// This will be null if there are no later Posts
		/// </summary>
		public Post NextPostIfAny { get; private set; }
		
		public PostMatchTypeOptions PostMatchType { get; private set; }

		public enum PostMatchTypeOptions
		{
			Alias,
			PreciseMatch
		}
	}
}