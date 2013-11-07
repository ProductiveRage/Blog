using System;
using System.Linq;
using FullTextIndexer.Common.Lists;

namespace BlogBackEnd.Models
{
	[Serializable]
	public class Post : PostStub
	{
		public Post(int id, DateTime posted, DateTime lastModified, string slug, string title, bool isHighlight, string markdownContent, NonNullImmutableList<string> tags)
			: base(id, posted, lastModified, slug, title, isHighlight)
		{
			if (string.IsNullOrWhiteSpace(markdownContent))
				throw new ArgumentException("Null/blank markdownContent content");
			if (tags == null)
				throw new ArgumentNullException("tags");
			if (tags.Any(t => t.Trim() == ""))
				throw new ArgumentException("Blank tag specified");

			MarkdownContent = markdownContent;
			Tags = new NonNullOrEmptyStringList(tags.Distinct());
		}

		/// <summary>
		/// This will never return null or empty
		/// </summary>
		public string MarkdownContent { get; private set; }

		/// <summary>
		/// This will never return null nor any (case-sensitive) duplicates
		/// </summary>
		public NonNullOrEmptyStringList Tags { get; private set; }
	}
}
