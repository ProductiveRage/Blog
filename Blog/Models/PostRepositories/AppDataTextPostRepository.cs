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
		public NonNullImmutableList<Post> Get(string tag)
		{
			tag = (tag ?? "").Trim();
			if (tag == "")
				throw new ArgumentException("Null/empty tag specified");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => p.Tags.Contains(tag, StringComparer.InvariantCultureIgnoreCase)).OrderByDescending(p => p.Posted)
			);
		}

		/// <summary>
		/// min is inclusive, max is not
		/// </summary>
		public NonNullImmutableList<Post> Get(DateTime min, DateTime max)
		{
			if (min > max)
				throw new ArgumentOutOfRangeException("min", "must be <= max");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => p.Posted >= min && p.Posted <= max).OrderByDescending(p => p.Posted)
			);
		}

		public NonNullImmutableList<Post> Get(ImmutableList<int> ids)
		{
			if (ids == null)
				throw new ArgumentNullException("ids");

			return new NonNullImmutableList<Post>(
				_postRetriever.Get().Where(p => ids.Contains(p.Id)).OrderByDescending(p => p.Posted)
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
