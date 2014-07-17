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
				return new HttpNotFoundResult();

			Response.ContentType = "text/xml";
			return View(
				new RSSFeedModel(
					posts
						.Sort((x, y) => -x.Posted.CompareTo(y.Posted))
						.Take(_maximumNumberOfPostsToPublish)
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
						.ToNonNullImmutableList(),
					new PostSlugRetriever(posts),
					_cache
				)
			);
		}

		private class PostSlugRetriever : IRetrievePostSlugs
		{
			private readonly NonNullImmutableList<Post> _posts;
			public PostSlugRetriever(NonNullImmutableList<Post> posts)
			{
				if (posts == null)
					throw new ArgumentNullException("posts");

				_posts = posts;
			}

			/// <summary>
			/// This will never return null or blank, it will raise an exception if the id is invalid or if otherwise unable to satisfy the request
			/// </summary>
			public string GetSlug(int postId)
			{
				var post = _posts.FirstOrDefault(p => p.Id == postId);
				if (post == null)
					throw new ArgumentException("Invalid postId: " + postId);
				return post.Slug;
			}
		}
	}
}
