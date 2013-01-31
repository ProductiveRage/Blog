using System;

namespace Blog.Models
{
	public class ArchiveMonthLink
	{
		public ArchiveMonthLink(string displayText, int month, int year, int postCount)
		{
			displayText = (displayText ?? "").Trim();
			if (displayText == "")
				throw new ArgumentException("Null/blank displayText specified");
			if ((month < 1) || (month > 12))
				throw new ArgumentOutOfRangeException("month");
			if ((year < 1990) || (year > 2100))
				throw new ArgumentOutOfRangeException("year");
			if (postCount <= 0)
				throw new ArgumentOutOfRangeException("postCount");

			DisplayText = displayText;
			Month = month;
			Year = year;
			PostCount = postCount;
		}
		public string DisplayText { get; private set; }
		public int Month { get; private set; }
		public int Year { get; private set; }
		public int PostCount { get; private set; }
	}
}
