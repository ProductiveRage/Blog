using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.FullTextIndexing;
using FullTextIndexer.Common.Lists;
using Microsoft.AspNetCore.Mvc;

namespace Blog.Controllers
{
    public sealed class SearchController : AbstractContentDeliveringController
    {
		private readonly IPostRepository _postRepository;
		private readonly IPostIndexer _postIndexer;
		private readonly SiteConfiguration _siteConfiguration;
		private readonly ICache _postContentCache;
		public SearchController(IPostRepository postRepository, IPostIndexer postIndexer, SiteConfiguration siteConfiguration, ICache postContentCache)
		{
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
			_postIndexer = postIndexer ?? throw new ArgumentNullException(nameof(postIndexer));
			_siteConfiguration = siteConfiguration ?? throw new ArgumentNullException(nameof(siteConfiguration));
			_postContentCache = postContentCache ?? throw new ArgumentNullException(nameof(postContentCache));
		}

		public async Task<IActionResult> Search(string term)
		{
			term = (term ?? "").Trim();

			IEnumerable<SearchResult> results;
			if (term == "")
				results = Array.Empty<SearchResult>();
			else
			{
				var allPosts = await _postRepository.GetAll();
				results = _postIndexer.GenerateIndexContent(allPosts).Search(term).Select(
					m => new SearchResult(allPosts.First(p => p.Id == m.Key), m.Weight, m.SourceLocationsIfRecorded)
				);
			}

			return View(
				"Search",
				new SearchResultsModel(
					term,
					results.ToNonNullImmutableList(),
					await _postRepository.GetMostRecentStubs(5),
					await _postRepository.GetStubs(null, null, true),
					await _postRepository.GetArchiveLinks(),
					_siteConfiguration.OptionalCanonicalLinkBase,
					_siteConfiguration.OptionalGoogleAnalyticsId,
					_postContentCache
				)
			);
		}

		public async Task<IActionResult> GetAutoCompleteContent()
		{
			var posts = await _postRepository.GetAll();
			var lastModifiedDateOfData = posts.Any() ? posts.Max(p => p.LastModified) : DateTime.MinValue;
			var lastModifiedDateFromRequest = base.TryToGetIfModifiedSinceDateFromRequest();
			if ((lastModifiedDateFromRequest != null) && (Math.Abs(lastModifiedDateFromRequest.Value.Subtract(lastModifiedDateOfData).TotalSeconds) < 2))
			{
				// Add a small grace period to the comparison (if only because lastModifiedDateOfLiveData is granular to milliseconds while lastModifiedDate only
				// considers seconds and so will nearly always be between zero and one seconds older)
				Response.StatusCode = 304;
				return Json("{ \"Result\": \"Not Modified\" }");
			}

			base.SetResponseCacheHeadersForSuccess(lastModifiedDateOfData);
			return Json(_postIndexer.GenerateIndexContent(posts).AutoCompleteContent);
		}
	}
}