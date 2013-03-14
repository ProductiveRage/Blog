using System;
using System.Collections.Generic;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
	[Serializable]
	public class CachedPostIndexContent
	{
		private readonly Dictionary<int, DateTime> _sourcePostSummary;
		public CachedPostIndexContent(PostIndexContent index, NonNullImmutableList<Post> posts)
		{
			if (index == null)
				throw new ArgumentNullException("index");
			if (posts == null)
				throw new ArgumentNullException("post");

			_sourcePostSummary = new Dictionary<int, DateTime>();
			foreach (var post in posts)
			{
				if (_sourcePostSummary.ContainsKey(post.Id))
					throw new ArgumentException("Duplicate Post Id encountered: " + post.Id);
				_sourcePostSummary.Add(post.Id, post.LastModified);
			}

			Index = index;
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public PostIndexContent Index { get; private set; }

		/// <summary>
		/// This will throw an exception for a null posts reference
		/// </summary>
		public bool IsValidForPostsData(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException("post");

			foreach (var post in posts)
			{
				if (!_sourcePostSummary.ContainsKey(post.Id))
					return false;

				if (_sourcePostSummary[post.Id] < post.LastModified)
					return false;
			}
			return true;
		}
	}
}
