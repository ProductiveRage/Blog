using System;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;

namespace Blog.Models
{
    [Serializable]
	public sealed class SearchResult
	{
		public SearchResult(Post post, float weight, NonNullImmutableList<SourceFieldLocation> sourceLocations)
		{
            if (weight <= 0)
				throw new ArgumentOutOfRangeException(nameof(weight));
			if (sourceLocations == null)
				throw new ArgumentNullException(nameof(sourceLocations));
			if (!sourceLocations.Any())
				throw new ArgumentException("Empty sourceLocations set specified - invalid");

			Post = post ?? throw new ArgumentNullException(nameof(post));
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
		public NonNullImmutableList<SourceFieldLocation> SourceLocations { get; private set; }
	}
}
