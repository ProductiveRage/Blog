﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using Microsoft.AspNetCore.Mvc;

namespace Blog.Controllers
{
	public sealed class ViewPostController : Controller
	{
		private readonly IPostRepository _postRepository;
		private readonly SiteConfiguration _siteConfiguration;
		private readonly ICache _postContentCache;
		public ViewPostController(IPostRepository postRepository, SiteConfiguration siteConfiguration, ICache postContentCache)
		{
			_postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
			_siteConfiguration = siteConfiguration ?? throw new ArgumentNullException(nameof(siteConfiguration));
			_postContentCache = postContentCache ?? throw new ArgumentNullException(nameof(postContentCache));
		}

		public async Task<IActionResult> ArchiveById(int? id)
		{
			if (id == null)
				return NotFound();

			var post = (await _postRepository.GetByIds(new ImmutableList<int>(id.Value))).FirstOrDefault();
			if (post == null)
				return NotFound();

			return RedirectToActionPermanent(
				"ArchiveBySlug",
				new { post.Slug }
			);
		}

		public async Task<IActionResult> ArchiveBySlug(string slug)
		{
			if (string.IsNullOrWhiteSpace(slug))
				return NotFound();

			var postMatch = await _postRepository.GetBySlug(slug);
			if (postMatch == null)
				return NotFound();

			if (postMatch.PostMatchType == PostMatchDetails.PostMatchTypeOptions.Alias)
			{
				return RedirectToActionPermanent(
					"ArchiveBySlug",
					new { postMatch.Post.Slug }
				);
			}

			// Try to extract a  description / intro from the Post content - the ideal is to find the first paragraph of content and break that at a natural point to get
			// no more than 200 characters. If that fails for then just try to get all of the plain text content and cut it at 200 characters (this is not as good an
			// approach because the plain text content may include code sample content if that appears very early on in the post).
			var description = postMatch.Post.TryToGetFirstParagraphContentAsPlainText(maxLength: 200);
			if (description == null)
			{
				description = postMatch.Post.GetContentAsPlainText();
				if (description.Length > 200)
					description = description.Substring(0, 198) + "..";
				else if (description == "")
					description = null;
			}

			TwitterCardDetails twitterCardDetails;
			if ((_siteConfiguration.OptionalTwitterUserName == null) || (_siteConfiguration.OptionalTwitterImage == null))
				twitterCardDetails = null;
			else
			{
				twitterCardDetails = new TwitterCardDetails(
					_siteConfiguration.OptionalTwitterUserName,
					postMatch.Post.Title,
					_siteConfiguration.OptionalTwitterImage,
					description ?? "No preview content available"
				);
			}

			return View(
				"Index",
				new PostListModel(
					postMatch.Post.Title,
					await GetPostsWithRelatedPostStubs(new[] { postMatch.Post }),
					postMatch.PreviousPostIfAny,
					postMatch.NextPostIfAny,
					await _postRepository.GetMostRecentStubs(5),
					await _postRepository.GetStubs(null, null, true),
					await _postRepository.GetArchiveLinks(),
					PostListDisplayOptions.SinglePost,
					_siteConfiguration.OptionalCanonicalLinkBase,
					_siteConfiguration.GetGoogleAnalyticsIdIfAny(Request),
					_siteConfiguration.OptionalDisqusShortName,
					description,
					twitterCardDetails,
					new PostSlugRetriever(_postRepository),
					_postContentCache
				)
			);
		}

		public async Task<IActionResult> ArchiveByTag(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
				return NotFound();

			var postsToDisplay = await _postRepository.GetByTag(tag);
			if (!postsToDisplay.Any())
				return NotFound();

			return View(
				"Index",
				new PostListModel(
					tag.Trim(),
					await GetPostsWithRelatedPostStubs(postsToDisplay),
					null, // previousPostIfAny,
					null, // nextPostIfAny
					await _postRepository.GetMostRecentStubs(5),
					await _postRepository.GetStubs(null, null, true),
					await _postRepository.GetArchiveLinks(),
					PostListDisplayOptions.ArchiveByTag,
					_siteConfiguration.OptionalCanonicalLinkBase,
					_siteConfiguration.GetGoogleAnalyticsIdIfAny(Request),
					_siteConfiguration.OptionalDisqusShortName,
					optionalMetaDescription: "Archive for tag: " + tag,
					null, // optionalTwitterCardDetails
					new PostSlugRetriever(_postRepository),
					_postContentCache
				)
			);
		}

		public async Task<IActionResult> ArchiveByMonth(int? month, int? year)
		{
			var valid = (month != null) && (month.Value >= 1) && (month.Value <= 12) && (year != null);
			if (!valid)
				return NotFound();

			var startDate = new DateTime(year.Value, month.Value, 1);
			var posts = await _postRepository.GetByDateRange(startDate, startDate.AddMonths(1));
			if (!posts.Any())
				return NotFound();

			var monthDisplayText = new DateTime(year.Value, month.Value, 1).ToString("MMMM yyyy");
			return View(
				"Index",
				new PostListModel(
					monthDisplayText,
					await GetPostsWithRelatedPostStubs(posts),
					null, // previousPostIfAny,
					null, // nextPostIfAny
					await _postRepository.GetMostRecentStubs(5),
					await _postRepository.GetStubs(null, null, true),
					await _postRepository.GetArchiveLinks(),
					PostListDisplayOptions.ArchiveByMonth,
					_siteConfiguration.OptionalCanonicalLinkBase,
					_siteConfiguration.GetGoogleAnalyticsIdIfAny(Request),
					_siteConfiguration.OptionalDisqusShortName,
					optionalMetaDescription: "Archive for " + monthDisplayText,
					null, // optionalTwitterCardDetails
					new PostSlugRetriever(_postRepository),
					_postContentCache
				)
			);
		}

		public async Task<IActionResult> ArchiveByTitle()
		{
			var posts = await _postRepository.GetAll();
			if (!posts.Any())
				return NotFound();

			return View(
				"PostsByTitle",
				new PostListModel(
					"Every Post Title",
					posts
						.Select(post => new PostWithRelatedPostStubs(
							post.Id,
							post.Posted,
							post.LastModified,
							post.Slug,
							post.RedirectFromSlugs,
							post.Title,
							post.IsHighlight,
							post.MarkdownContent,
							relatedPosts: NonNullImmutableList<PostStub>.Empty, // Don't need related Post data here
							autoSuggestedRelatedPosts: NonNullImmutableList<PostStub>.Empty,
							post.Tags
						))
						.OrderByDescending(post => post.Posted)
						.ToNonNullImmutableList(),
					previousPostIfAny: null,
					nextPostIfAny: null,
					await _postRepository.GetMostRecentStubs(5),
					await _postRepository.GetStubs(null, null, true),
					await _postRepository.GetArchiveLinks(),
					PostListDisplayOptions.ArchiveByEveryTitle,
					_siteConfiguration.OptionalCanonicalLinkBase,
					_siteConfiguration.GetGoogleAnalyticsIdIfAny(Request),
					_siteConfiguration.OptionalDisqusShortName,
					optionalMetaDescription: "Every Post Title on this site",
					optionalTwitterCardDetails: null,
					new PostSlugRetriever(_postRepository),
					_postContentCache
				)
			);
		}

		public async Task<IActionResult> ArchiveByMonthMostRecent()
		{
			var mostRecentPostDate = await _postRepository.GetMaxPostDate();
			if (mostRecentPostDate == null)
				return NotFound();
			return await ArchiveByMonth(mostRecentPostDate.Value.Month, mostRecentPostDate.Value.Year);
		}

		private async Task<PostWithRelatedPostStubs> GetPostWithRelatedPostStubs(Post post)
		{
			if (post == null)
				throw new ArgumentNullException(nameof(post));
			
			return new PostWithRelatedPostStubs(
				post.Id,
				post.Posted,
				post.LastModified,
				post.Slug,
				post.RedirectFromSlugs,
				post.Title,
				post.IsHighlight,
				post.MarkdownContent,
				await GetRelatedPostStubs(post.RelatedPosts),
				await GetRelatedPostStubs(post.AutoSuggestedRelatedPosts),
				post.Tags
			);

			async Task<NonNullImmutableList<PostStub>> GetRelatedPostStubs(ImmutableList<int> ids)
			{
				var orderValues = ids
					.Select((id, index) => (Id: id, Index: index))
					.ToDictionary(entry => entry.Id, entry => entry.Index);

				return (await _postRepository.GetByIds(ids))
					.Cast<PostStub>()
					.OrderBy(p => orderValues.TryGetValue(p.Id, out var orderValue) ? orderValue : int.MaxValue)
					.ToNonNullImmutableList();
			}
		}

		private async Task<NonNullImmutableList<PostWithRelatedPostStubs>> GetPostsWithRelatedPostStubs(IEnumerable<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException(nameof(posts));

			return (await Task.WhenAll(posts.Select(p => GetPostWithRelatedPostStubs(p)))).ToNonNullImmutableList();
		}

		private class PostSlugRetriever : IRetrievePostSlugs
		{
			private readonly IPostRepository _postRepository;
			public PostSlugRetriever(IPostRepository postRepository)
			{
				_postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
			}

			/// <summary>
			/// This will never return null or blank, it will raise an exception if the id is invalid or if otherwise unable to satisfy the request
			/// </summary>
			public async Task<string> GetSlug(int postId)
			{
				var post = (await _postRepository.GetByIds(new ImmutableList<int>(postId))).FirstOrDefault(p => p.Id == postId);
				if (post == null)
					throw new ArgumentException("Invalid postId: " + postId);
				return post.Slug;
			}
		}
	}
}
