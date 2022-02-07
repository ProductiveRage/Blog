using System;
using System.Linq;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public sealed class PostWithRelatedPostStubs : Post
	{
		public PostWithRelatedPostStubs(
			int id,
			DateTime posted,
			DateTime lastModified,
			string slug,
			NonNullOrEmptyStringList redirectFromSlugs,
			string title,
			bool isHighlight,
			string markdownContent,
			NonNullImmutableList<PostStub> relatedPosts,
			NonNullImmutableList<PostStub> autoSuggestedRelatedPosts,
			NonNullImmutableList<TagSummary> tags)
			: base(
				id,
				posted,
				lastModified,
				slug,
				redirectFromSlugs,
				title,
				isHighlight,
				markdownContent,
				(relatedPosts ?? NonNullImmutableList<PostStub>.Empty).Select(p => p.Id).ToImmutableList(),
				(autoSuggestedRelatedPosts ?? NonNullImmutableList<PostStub>.Empty).Select(p => p.Id).ToImmutableList(),
				tags)
		{
            RelatedPosts = relatedPosts ?? throw new ArgumentNullException(nameof(relatedPosts));
			AutoSuggestedRelatedPosts = autoSuggestedRelatedPosts ?? throw new ArgumentNullException(nameof(autoSuggestedRelatedPosts));
		}

		/// <summary>
		/// This will never be null (though it may be an empty set)
		/// </summary>
		public new NonNullImmutableList<PostStub> RelatedPosts { get; }

		/// <summary>
		/// This will never be null (though it may be an empty set)
		/// </summary>
		public new NonNullImmutableList<PostStub> AutoSuggestedRelatedPosts { get; }
	}
}