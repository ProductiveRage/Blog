using System;

namespace BlogBackEnd.Models
{
    [Serializable]
	public sealed class TagSummary
	{
		public TagSummary(string tag, int numberOfPosts)
		{
			if (string.IsNullOrWhiteSpace(tag))
				throw new ArgumentException("Null/blank tag specified");
			if (numberOfPosts <= 0)
				throw new ArgumentOutOfRangeException(nameof(numberOfPosts));

			Tag = tag.Trim();
			NumberOfPosts = numberOfPosts;
		}

		/// <summary>
		/// This will never be null, blank or whitespace only. It will never have any leading or trailing whitespace.
		/// </summary>
		public string Tag { get; private set; }

		/// <summary>
		/// This will always be a positive value
		/// </summary>
		public int NumberOfPosts { get; private set; }
	}
}
