using System;
using System.Linq;
using System.Threading.Tasks;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using Microsoft.AspNetCore.Mvc;

namespace Blog.Controllers
{
    public class RSSController : Controller
	{
		private readonly IPostRepository _postRepository;
		private readonly int _maximumNumberOfPostsToPublish;
		private readonly ICache _postContentCache;
		public RSSController(IPostRepository postRepository, SiteConfiguration siteConfiguration, ICache postContentCache)
		{
			if (postRepository == null)
				throw new ArgumentNullException("postRepository");
			if (siteConfiguration == null)
				throw new ArgumentNullException("siteConfiguration");
			if (postContentCache == null)
				throw new ArgumentNullException("postContentCache");

			_postRepository = postRepository;
			_maximumNumberOfPostsToPublish = siteConfiguration.MaximumNumberOfPostsToPublishInRssFeed;
			_postContentCache = postContentCache;
		}

		public async Task<IActionResult> Feed()
		{
			var posts = await _postRepository.GetAll();
			if (!posts.Any())
				return NotFound();

			Response.ContentType = "text/xml";
			var postsForFeed = await Task.WhenAll(
				posts
					.Sort((x, y) => -x.Posted.CompareTo(y.Posted))
					.Take(_maximumNumberOfPostsToPublish)
					.Select(async post => new PostWithRelatedPostStubs(
							post.Id,
							post.Posted,
							post.LastModified,
							post.Slug,
							post.RedirectFromSlugs,
							post.Title,
							post.IsHighlight,
							post.MarkdownContent,
							(await _postRepository.GetByIds(post.RelatedPosts)).Cast<PostStub>().ToNonNullImmutableList(),
							post.Tags
						))
			);
			return View(
				new RSSFeedModel(
					postsForFeed.ToNonNullImmutableList(),
					new PostSlugRetriever(posts),
					_postContentCache
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
			public Task<string> GetSlug(int postId)
			{
				var post = _posts.FirstOrDefault(p => p.Id == postId);
				if (post == null)
					throw new ArgumentException("Invalid postId: " + postId);
				return Task.FromResult(post.Slug);
			}
		}
	}
}
