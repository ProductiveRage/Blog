using System;
using System.Linq;
using System.Threading.Tasks;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public static class IPostRepository_Extensions
	{
		public static async Task<NonNullImmutableList<ArchiveMonthLink>> GetArchiveLinks(this IPostRepository repo)
		{
			if (repo == null)
				throw new ArgumentNullException(nameof(repo));

			var months = NonNullImmutableList<ArchiveMonthLink>.Empty;
			var min = await repo.GetMinPostDate();
			var max = await repo.GetMaxPostDate();
			if ((min != null) && (max != null))
			{
				var startDate = new DateTime(max.Value.Year, max.Value.Month, 1);
				while (true)
				{
					var postCount = (await repo.GetByDateRange(startDate, startDate.AddMonths(1))).Count();
					if (postCount > 0)
					{
						months = months.Add(new ArchiveMonthLink(
						  startDate.ToString("MMMM yyyy"),
						  startDate.Month,
						  startDate.Year,
						  postCount
						));
					}

					if (startDate <= min)
						break;

					startDate = startDate.AddMonths(-1);
				}
			}
			return months;
		}
	}
}
