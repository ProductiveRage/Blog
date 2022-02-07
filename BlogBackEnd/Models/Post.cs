using System;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.Models
{
    [Serializable]
	public class Post : PostStub
	{
		public Post(
			int id,
			DateTime posted,
			DateTime lastModified,
			string slug,
			NonNullOrEmptyStringList redirectFromSlugs,
			string title,
			bool isHighlight,
			string markdownContent,
			ImmutableList<int> relatedPosts,
			ImmutableList<int> autoSuggestedRelatedPosts,
			NonNullImmutableList<TagSummary> tags)
			: base(id, posted, lastModified, slug, title, isHighlight)
		{
            if (string.IsNullOrWhiteSpace(markdownContent))
				throw new ArgumentException("Null/blank markdownContent content");

            RedirectFromSlugs = redirectFromSlugs ?? throw new ArgumentNullException(nameof(redirectFromSlugs));
			MarkdownContent = markdownContent;
			RelatedPosts = relatedPosts ?? throw new ArgumentNullException(nameof(relatedPosts));
			AutoSuggestedRelatedPosts = autoSuggestedRelatedPosts ?? throw new ArgumentNullException(nameof(autoSuggestedRelatedPosts));
			Tags = tags ?? throw new ArgumentNullException(nameof(tags));
		}

		/// <summary>
		/// This will never be null (but may be empty if there are no redirects for this Post)
		/// </summary>
		public NonNullOrEmptyStringList RedirectFromSlugs { get; private set; }

		/// <summary>
		/// This will never return null or empty
		/// </summary>
		public string MarkdownContent { get; }

		/// <summary>
		/// This will never be null (though it may be an empty set)
		/// </summary>
		public ImmutableList<int> RelatedPosts { get; }

		/// <summary>
		/// This will never be null (though it may be an empty set)
		/// </summary>
		public ImmutableList<int> AutoSuggestedRelatedPosts { get; }

		/// <summary>
		/// This will never return null nor any (case-sensitive) duplicates
		/// </summary>
		public NonNullImmutableList<TagSummary> Tags { get; }
	}
}
