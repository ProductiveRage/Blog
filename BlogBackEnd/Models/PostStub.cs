using System;
using System.Collections.Generic;
using System.Linq;

namespace BlogBackEnd.Models
{
	[Serializable]
	public class PostStub
	{
		public PostStub(int id, DateTime posted, DateTime lastModified, string slug, string title, bool isHighlight)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new ArgumentException("Null/blank title content");

			Id = id;
			Posted = posted;
			LastModified = lastModified;
			Slug = slug.Trim();
			Title = title.Trim();
			IsHighlight = isHighlight;
		}

		public int Id { get; private set; }

		public DateTime Posted { get; private set; }

		public DateTime LastModified { get; private set; }

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string Slug { get; private set; }

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string Title { get; private set; }

		public bool IsHighlight { get; private set; }
	}
}
