using System;
using System.Linq;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;

namespace Blog.Models
{
	public class AppDataTextPostRepository : IPostRepository
	{
		private ISingleFolderPostRetriever _postRetriever;
		public AppDataTextPostRepository(ISingleFolderPostRetriever postRetriever)
		{
			if (postRetriever == null)
				throw new ArgumentNullException("postRetriever");

			_postRetriever = postRetriever;
		}

		public NonNullImmutableList<Post> GetAll()
		{
			return _postRetriever.Get();
		}

		/// <summary>
		/// This is case insensitive
		/// </summary>
		public NonNullImmutableList<Post> GetByTag(string tag)
		{
			tag = (tag ?? "").Trim();
			if (tag == "")
				throw new ArgumentException("Null/empty tag specified");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => p.Tags.Any(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))).OrderByDescending(p => p.Posted)
			);
		}

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		public NonNullImmutableList<Post> GetByDateRange(DateTime min, DateTime max)
		{
			if (min > max)
				throw new ArgumentOutOfRangeException("min", "must be <= max");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => p.Posted >= min && p.Posted <= max).OrderByDescending(p => p.Posted)
			);
		}

		public NonNullImmutableList<Post> GetByIds(ImmutableList<int> ids)
		{
			if (ids == null)
				throw new ArgumentNullException("ids");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => ids.Contains(p.Id)).OrderByDescending(p => p.Posted)
			);
		}

		/// <summary>
		/// This is case sensitive, it will return null if the slug is invalid
		/// </summary>
		public PostMatchDetails GetBySlug(string slug)
		{
			slug = (slug ?? "").Trim();
			if (slug == "")
				throw new ArgumentException("Null/empty slug specified");

			var allPosts = _postRetriever.Get();
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
		public NonNullImmutableList<PostStub> GetStubs(DateTime? min, DateTime? max, bool highlightsOnly)
		{
			return new NonNullImmutableList<PostStub>(
				_postRetriever.Get().Where(p =>
					(min == null || p.Posted >= min) &&
					(max == null || p.Posted < max) &&
					(!highlightsOnly || p.IsHighlight)
				).OrderByDescending(p => p.Posted)
			);
		}

		public NonNullImmutableList<PostStub> GetMostRecentStubs(int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException("count", "must be >= 0");
			return new NonNullImmutableList<PostStub>(
				_postRetriever.Get().OrderByDescending(p => p.Posted).Take(count)
			);
		}

		public NonNullImmutableList<PostStub> GetHighlights()
		{
			return new NonNullImmutableList<PostStub>(
				_postRetriever.Get().Where(p => p.IsHighlight).OrderByDescending(p => p.Posted)
			);
		}

		public DateTime? GetMinPostDate()
		{
			var posts = _postRetriever.Get();
			if (posts.Count() == 0)
				return null;
			return posts.Min(p => p.Posted);
		}

		public DateTime? GetMaxPostDate()
		{
			var posts = _postRetriever.Get(); ;
			if (posts.Count() == 0)
				return null;
			return posts.Max(p => p.Posted);
		}
	}
}
