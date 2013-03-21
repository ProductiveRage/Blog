using System;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;

namespace Blog.Models
{
	[Serializable]
	public class SearchResult
	{
		public SearchResult(Post post, float weight, NonNullImmutableList<SourceFieldLocationWithTerm> sourceLocations)
		{
			if (post == null)
				throw new ArgumentNullException("post");
			if (weight <= 0)
				throw new ArgumentOutOfRangeException("weight");
			if (sourceLocations == null)
				throw new ArgumentNullException("sourceLocations");
			if (!sourceLocations.Any())
				throw new ArgumentException("Empty sourceLocations set specified - invalid");

			Post = post;
			Weight = weight;
			SourceLocations = sourceLocations;
		}

		/// <summary>
		/// This will be null
		/// </summary>
		public Post Post { get; private set; }

		/// <summary>
		/// This will always be greater than zero
		/// </summary>
		public float Weight { get; private set; }

		/// <summary>
		/// This will never be null nor empty
		/// </summary>
		public NonNullImmutableList<SourceFieldLocationWithTerm> SourceLocations { get; private set; }
	}
}
