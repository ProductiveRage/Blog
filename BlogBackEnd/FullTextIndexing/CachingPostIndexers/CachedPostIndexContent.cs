using System;
using System.Collections.Generic;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
    [Serializable]
	public sealed class CachedPostIndexContent
	{
		private readonly Dictionary<int, DateTime> _sourcePostSummary;
		public CachedPostIndexContent(PostIndexContent index, NonNullImmutableList<Post> posts)
		{
			_sourcePostSummary = new Dictionary<int, DateTime>();
			foreach (var post in posts ?? throw new ArgumentNullException(nameof(posts)))
			{
				if (_sourcePostSummary.ContainsKey(post.Id))
					throw new ArgumentException("Duplicate Post Id encountered: " + post.Id);
				_sourcePostSummary.Add(post.Id, post.LastModified);
			}
			Index = index ?? throw new ArgumentNullException(nameof(index));
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
			foreach (var post in posts ?? throw new ArgumentNullException(nameof(posts)))
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
