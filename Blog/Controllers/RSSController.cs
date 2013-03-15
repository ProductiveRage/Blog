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
	public class RSSController : AbstractErrorLoggingController
	{
		private readonly IPostRepository _postRepository;
		private readonly int _maximumNumberOfPostsToPublish;
		private readonly ICache _cache;
		public RSSController(IPostRepository postRepository, int maximumNumberOfPostsToPublish, ICache cache)
		{
			if (postRepository == null)
				throw new ArgumentNullException("postRepository");
			if (maximumNumberOfPostsToPublish <= 0)
				throw new ArgumentOutOfRangeException("maximumNumberOfPostsToPublish", "must be greater than zero");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_postRepository = postRepository;
			_maximumNumberOfPostsToPublish = maximumNumberOfPostsToPublish;
			_cache = cache;
		}

		[Stopwatch]
		public ActionResult Feed()
		{
			var posts = _postRepository.GetAll();
			if (!posts.Any())
				throw new HttpException(404, "No content for RSS Feed");

			Response.ContentType = "text/xml";
			return View(
				new RSSFeedModel(
					posts.Sort((x, y) => -x.Posted.CompareTo(y.Posted)).Take(_maximumNumberOfPostsToPublish).ToNonNullImmutableList(),
					_cache
				)
			);
		}
	}
}
