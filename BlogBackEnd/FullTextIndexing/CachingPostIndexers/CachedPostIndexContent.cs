using System;
using System.Collections.Generic;
using BlogBackEnd.Models;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.FullTextIndexing.CachingPostIndexers
{
	[Serializable]
	public class CachedPostIndexContent : PostIndexContent
	{
		private Dictionary<int, DateTime> _sourcePostSummary;
		public CachedPostIndexContent(IIndexData<int> searchIndex, NonNullOrEmptyStringList autoCompleteContent, NonNullImmutableList<Post> posts) : base(searchIndex, autoCompleteContent)
		{
			if (posts == null)
				throw new ArgumentNullException("post");

			_sourcePostSummary = new Dictionary<int, DateTime>();
			foreach (var post in posts)
			{
				if (_sourcePostSummary.ContainsKey(post.Id))
					throw new ArgumentException("Duplicate Post Id encountered: " + post.Id);
				_sourcePostSummary.Add(post.Id, post.LastModified);
			}
		}

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
