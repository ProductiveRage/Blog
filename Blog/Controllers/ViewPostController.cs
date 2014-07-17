using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Blog.Helpers.Timing;
using Blog.Models;
using BlogBackEnd.Caching;
using FullTextIndexer.Common.Lists;
using BlogBackEnd.Models;

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
				return new HttpNotFoundResult();

			var post = _postRepository.GetByIds(new[] { id.Value }.ToImmutableList()).FirstOrDefault();
			if (post == null)
				return new HttpNotFoundResult();

			return RedirectToActionPermanent(
				"ArchiveBySlug",
				new { Slug = post.Slug }
			);
		}

		[Stopwatch]
		public ActionResult ArchiveBySlug(string slug)
		{
			if (string.IsNullOrWhiteSpace(slug))
				return new HttpNotFoundResult();

			var postMatch = _postRepository.GetBySlug(slug);
			if (postMatch == null)
				return new HttpNotFoundResult();

			if (postMatch.PostMatchType == PostMatchDetails.PostMatchTypeOptions.Alias)
			{
				return RedirectToActionPermanent(
					"ArchiveBySlug",
					new { Slug = postMatch.Post.Slug }
				);
			}

			return View(
				"Index",
				new PostListModel(
					postMatch.Post.Title,
					GetPostsWithRelatedPostStubs(
						new NonNullImmutableList<Post>(new[] { postMatch.Post })
					),
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					true,
					_optionalCanonicalLinkBase,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					new PostSlugRetriever(_postRepository),
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByTag(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
				return new HttpNotFoundResult();

			return View(
				"Index",
				new PostListModel(
					tag.Trim(),
					GetPostsWithRelatedPostStubs(
						_postRepository.GetByTag(tag)
					),
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					false,
					_optionalCanonicalLinkBase,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					new PostSlugRetriever(_postRepository),
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByMonth(int? month, int? year)
		{
			var valid = ((month != null) && (month.Value >= 1) && (month.Value <= 12) && (year != null));
			if (!valid)
				return new HttpNotFoundResult();

			var startDate = new DateTime(year.Value, month.Value, 1);
			var posts = _postRepository.GetByDateRange(startDate, startDate.AddMonths(1));
			if (posts.Count() == 0)
				return new HttpNotFoundResult();

			return View(
				"Index",
				new PostListModel(
					new DateTime(year.Value, month.Value, 1).ToString("MMMM yyyy"),
					GetPostsWithRelatedPostStubs(posts),
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					false,
					_optionalCanonicalLinkBase,
					_optionalGoogleAnalyticsId,
					_optionalDisqusShortName,
					new PostSlugRetriever(_postRepository),
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult ArchiveByMonthMostRecent()
		{
			var mostRecentPostDate = _postRepository.GetMaxPostDate();
			if (mostRecentPostDate == null)
				return new HttpNotFoundResult();
			return ArchiveByMonth(mostRecentPostDate.Value.Month, mostRecentPostDate.Value.Year);
		}

		private NonNullImmutableList<PostWithRelatedPostStubs> GetPostsWithRelatedPostStubs(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException("posts");

			return posts
				.Select(post => new PostWithRelatedPostStubs(
					post.Id,
					post.Posted,
					post.LastModified,
					post.Slug,
					post.RedirectFromSlugs,
					post.Title,
					post.IsHighlight,
					post.MarkdownContent,
					_postRepository.GetByIds(post.RelatedPosts).Cast<PostStub>().ToNonNullImmutableList(),
					post.Tags
				))
				.ToNonNullImmutableList();
		}

		private class PostSlugRetriever : IRetrievePostSlugs
		{
			private readonly IPostRepository _postRepository;
			public PostSlugRetriever(IPostRepository postRepository)
			{
				if (postRepository == null)
					throw new ArgumentNullException("postRepository");

				_postRepository = postRepository;
			}

			/// <summary>
			/// This will never return null or blank, it will raise an exception if the id is invalid or if otherwise unable to satisfy the request
			/// </summary>
			public string GetSlug(int postId)
			{
				var post = _postRepository.GetByIds(new[] { postId }.ToImmutableList()).FirstOrDefault(p => p.Id == postId);
				if (post == null)
					throw new ArgumentException("Invalid postId: " + postId);
				return post.Slug;
			}
		}
	}
}
