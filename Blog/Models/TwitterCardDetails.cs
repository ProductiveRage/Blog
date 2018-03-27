using System;

namespace Blog.Models
{
	public sealed class TwitterCardDetails
	{
		public TwitterCardDetails(string userName, string title, string image, string description)
		{
			if (string.IsNullOrWhiteSpace(userName))
				throw new ArgumentException($"Null/blank {nameof(userName)} specified");
			if (string.IsNullOrWhiteSpace(title))
				throw new ArgumentException($"Null/blank {nameof(title)} specified");
			if (string.IsNullOrWhiteSpace(image))
				throw new ArgumentException($"Null/blank {nameof(image)} specified");
			if (string.IsNullOrWhiteSpace(description))
				throw new ArgumentException($"Null/blank {nameof(description)} specified");

			UserName = userName.Trim();
			Title = title.Trim();
			Image = image.Trim();
			Description = description.Trim();
		}

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string UserName { get; }

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string Image { get; }

		/// <summary>
		/// This will never be null or empty nor have any leading or trailing whitespace
		/// </summary>
		public string Description { get; }
	}
}
