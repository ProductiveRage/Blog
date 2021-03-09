using System;
using System.Linq;
using System.Threading.Tasks;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
    public sealed class AppDataTextPostRepository : IPostRepository
	{
		private readonly ISingleFolderPostRetriever _postRetriever;
		public AppDataTextPostRepository(ISingleFolderPostRetriever postRetriever)
		{
            _postRetriever = postRetriever ?? throw new ArgumentNullException(nameof(postRetriever));
		}

		public Task<NonNullImmutableList<Post>> GetAll()
		{
			return _postRetriever.Get();
		}

		/// <summary>
		/// This is case insensitive
		/// </summary>
		public async Task<NonNullImmutableList<Post>> GetByTag(string tag)
		{
			tag = (tag ?? "").Trim();
			if (tag == "")
				throw new ArgumentException("Null/empty tag specified");

			return new NonNullImmutableList<Post>(
				(await _postRetriever.Get()).Where(p => p.Tags.Any(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))).OrderByDescending(p => p.Posted)
			);
		}

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		public async Task<NonNullImmutableList<Post>> GetByDateRange(DateTime min, DateTime max)
		{
			if (min > max)
				throw new ArgumentOutOfRangeException(nameof(min), "must be <= max");

			return new NonNullImmutableList<Post>(
				(await _postRetriever.Get()).Where(p => p.Posted >= min && p.Posted <= max).OrderByDescending(p => p.Posted)
			);
		}

		public async Task<NonNullImmutableList<Post>> GetByIds(ImmutableList<int> ids)
		{
			if (ids == null)
				throw new ArgumentNullException(nameof(ids));

			return new NonNullImmutableList<Post>(
				(await _postRetriever.Get()).Where(p => ids.Contains(p.Id)).OrderByDescending(p => p.Posted)
			);
		}

		/// <summary>
		/// This is case sensitive, it will return null if the slug is invalid
		/// </summary>
		public async Task<PostMatchDetails> GetBySlug(string slug)
		{
			slug = (slug ?? "").Trim();
			if (slug == "")
				throw new ArgumentException("Null/empty slug specified");

			var allPosts = await _postRetriever.Get();
			Post matchedPost;
			PostMatchDetails.PostMatchTypeOptions matchType;
			var preciseMatch = allPosts.FirstOrDefault(p => p.Slug == slug);
			if (preciseMatch != null)
			{
				matchedPost = preciseMatch;
				matchType = PostMatchDetails.PostMatchTypeOptions.PreciseMatch;
			}
			else
			{
				var alias = allPosts.FirstOrDefault(p => p.RedirectFromSlugs.Contains(slug));
				if (alias != null)
				{
					matchedPost = alias;
					matchType = PostMatchDetails.PostMatchTypeOptions.Alias;
				}
				else
					return null;
			}

			return new PostMatchDetails(
				matchedPost,
				allPosts.FirstOrDefault(p => p.Id == matchedPost.Id - 1),
				allPosts.FirstOrDefault(p => p.Id == matchedPost.Id + 1),
				matchType
			);
		}

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		public async Task<NonNullImmutableList<PostStub>> GetStubs(DateTime? min, DateTime? max, bool highlightsOnly)
		{
			return new NonNullImmutableList<PostStub>(
				(await _postRetriever.Get()).Where(p =>
					(min == null || p.Posted >= min) &&
					(max == null || p.Posted < max) &&
					(!highlightsOnly || p.IsHighlight)
				).OrderByDescending(p => p.Posted)
			);
		}

		public async Task<NonNullImmutableList<PostStub>> GetMostRecentStubs(int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), "must be >= 0");
			return new NonNullImmutableList<PostStub>(
				(await _postRetriever.Get()).OrderByDescending(p => p.Posted).Take(count)
			);
		}

		public async Task<NonNullImmutableList<PostStub>> GetHighlights()
		{
			return new NonNullImmutableList<PostStub>(
				(await _postRetriever.Get()).Where(p => p.IsHighlight).OrderByDescending(p => p.Posted)
			);
		}

		public async Task<DateTime?> GetMinPostDate()
		{
			var posts = await _postRetriever.Get();
			if (!posts.Any())
				return null;

			return posts.Min(p => p.Posted);
		}

		public async Task<DateTime?> GetMaxPostDate()
		{
			var posts = await _postRetriever.Get();
			if (!posts.Any())
				return null;

			return posts.Max(p => p.Posted);
		}
	}
}
