using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Blog.Helpers.Timing;
using Blog.Models;
using BlogBackEnd.Caching;
using FullTextIndexer.Common.Lists;

namespace Blog.Controllers
{
	public class ViewPostController : AbstractErrorLoggingController
	{
		private readonly IPostRepository _postRepository;
		private readonly string _optionalCanonicalLinkBase, _optionalGoogleAnalyticsId, _optionalDisqusShortName;
		private readonly ICache _cache;
		public ViewPostController(
			IPostRepository postRepository,
			string optionalCanonicalLinkBase,
			string optionalGoogleAnalyticsId,
			string optionalDisqusShortName,
			ICache cache)
		{
			if (postRepository == null)
				throw new ArgumentNullException("postRepository");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_postRepository = postRepository;
			_optionalCanonicalLinkBase = string.IsNullOrWhiteSpace(optionalCanonicalLinkBase) ? null : optionalCanonicalLinkBase.Trim();
			_optionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			_optionalDisqusShortName = string.IsNullOrWhiteSpace(optionalDisqusShortName) ? null : optionalDisqusShortName.Trim();
			_cache = cache;
		}

		[Stopwatch]
		public ActionResult ArchiveById(int? id)
		{
			if (id == null)
				throw new HttpException(404, "Not found");

			var post = _postRepository.Get(new[] { id.Value }.ToImmutableList()).FirstOrDefault();
			if (post == null)
				throw new HttpException(404, "Not found");

			return View(
				"Index",
				new PostListModel(
					post.Title,
					new[] { post },
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					true,
					_optionalCanonicalLinkBase,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByTag(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
				throw new HttpException(404, "Invalid tag");

			return View(
				"Index",
				new PostListModel(
					tag.Trim(),
					_postRepository.Get(tag),
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					false,
					_optionalCanonicalLinkBase,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByMonth(int? month, int? year)
		{
			var valid = ((month != null) && (month.Value >= 1) && (month.Value <= 12) && (year != null));
			if (!valid)
				throw new HttpException(404, "Not found");

			var startDate = new DateTime(year.Value, month.Value, 1);
			var posts = _postRepository.Get(startDate, startDate.AddMonths(1));
			if (posts.Count() == 0)
				throw new HttpException(404, "Not found");

			return View(
				"Index",
				new PostListModel(
					new DateTime(year.Value, month.Value, 1).ToString("MMMM yyyy"),
					posts,
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					false,
					_optionalDisqusShortName,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByMonthMostRecent()
		{
			var mostRecentPostDate = _postRepository.GetMaxPostDate();
			if (mostRecentPostDate == null)
				throw new HttpException(404, "Not found");
			return ArchiveByMonth(mostRecentPostDate.Value.Month, mostRecentPostDate.Value.Year);
		}
	}
}
