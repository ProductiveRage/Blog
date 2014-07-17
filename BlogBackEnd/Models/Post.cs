using System;
using System.Linq;
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
			NonNullOrEmptyStringList tags)
			: base(id, posted, lastModified, slug, title, isHighlight)
		{
			if (redirectFromSlugs == null)
				throw new ArgumentNullException("redirectFromSlugs");
			if (string.IsNullOrWhiteSpace(markdownContent))
				throw new ArgumentException("Null/blank markdownContent content");
			if (relatedPosts == null)
				throw new ArgumentNullException("relatedPostIds");
			if (tags == null)
				throw new ArgumentNullException("tags");
			if (tags.Any(t => t.Trim() == ""))
				throw new ArgumentException("Blank tag specified");

			RedirectFromSlugs = redirectFromSlugs;
			MarkdownContent = markdownContent;
			RelatedPosts = relatedPosts;
			Tags = tags;
		}

		/// <summary>
		/// This will never be null (but may be empty if there are no redirects for this Post)
		/// </summary>
		public NonNullOrEmptyStringList RedirectFromSlugs { get; private set; }

		/// <summary>
		/// This will never return null or empty
		/// </summary>
		public string MarkdownContent { get; private set; }

		/// <summary>
		/// This will never be null (though it may be an empty set)
		/// </summary>
		public ImmutableList<int> RelatedPosts { get; private set; }

		/// <summary>
		/// This will never return null nor any (case-sensitive) duplicates
		/// </summary>
		public NonNullOrEmptyStringList Tags { get; private set; }
	}
}
