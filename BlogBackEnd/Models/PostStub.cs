using System;
using System.Collections.Generic;
using System.Linq;

namespace BlogBackEnd.Models
{
	[Serializable]
	public class PostStub
	{
		public PostStub(int id, DateTime posted, DateTime lastModified, string title, bool isHighlight)
		{
			if (string.IsNullOrWhiteSpace(title))
				throw new ArgumentException("Null/blank title content");

			Id = id;
			Posted = posted;
			LastModified = lastModified;
			Title = title;
			IsHighlight = isHighlight;
		}

		public int Id { get; private set; }

		public DateTime Posted { get; private set; }

		public DateTime LastModified { get; private set; }

		/// <summary>
		/// This will never be null or empty
		/// </summary>
		public string Title { get; private set; }

		public bool IsHighlight { get; private set; }
	}
}
