﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Blog.Helpers.Timing;
using Blog.Misc;
using Blog.Models;
using BlogBackEnd.Caching;
using BlogBackEnd.FullTextIndexing;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Core.TokenBreaking;
using FullTextIndexer.Common.Lists;

namespace Blog.Controllers
{
	public class SearchController : AbstractContentDeliveringController
    {
		private IPostRepository _postRepository;
		private IPostIndexer _postIndexer;
		private string _optionalGoogleAnalyticsId;
		private ICache _cache;
		public SearchController(IPostRepository postRepository, IPostIndexer postIndexer, string optionalGoogleAnalyticsId, ICache cache)
		{
			if (postRepository == null)
				throw new ArgumentNullException("postRepository");
			if (postIndexer == null)
				throw new ArgumentNullException("postIndexer");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_postRepository = postRepository;
			_postIndexer = postIndexer;
			_optionalGoogleAnalyticsId = string.IsNullOrWhiteSpace(optionalGoogleAnalyticsId) ? null : optionalGoogleAnalyticsId.Trim();
			_cache = cache;
		}

		[ValidateInput(false)]
		[Stopwatch]
		public ActionResult Search(string term)
		{
			term = (term ?? "").Trim();

			IEnumerable<SearchResult> results;
			if (term == "")
				results = new SearchResult[0];
			else
			{
				var allPosts = _postRepository.GetAll();
				var index = _postIndexer.GenerateIndexContent(allPosts).SearchIndex;
				var matches = index.GetPartialMatches(
					term,
					new WhiteSpaceExtendingTokenBreaker(
						new ImmutableList<char>(new[] { '<', '>', '[', ']', '(', ')', '{', '}', '.', ',' }),
						new WhiteSpaceTokenBreaker()
					),
					(tokenMatches, allTokens) => (tokenMatches.Count < allTokens.Count) ? 0 : tokenMatches.SelectMany(m => m.Weights).Sum()
				);
				results = matches.Select(
					m => new SearchResult(allPosts.First(p => p.Id == m.Key), m.Weight)
				);
			}

			return View(
				"Search",
				new SearchResultsModel(
					term,
					results.ToNonNullImmutableList(),
					_postRepository.GetMostRecentStubs(5),
					_postRepository.GetStubs(null, null, true),
					_postRepository.GetArchiveLinks(),
					_optionalGoogleAnalyticsId,
					_cache
				)
			);
		}

		[Stopwatch]
		public ActionResult GetAutoCompleteContent()
		{
			var posts = _postRepository.GetAll();
			var lastModifiedDateOfData = posts.Any() ? posts.Max(p => p.LastModified) : DateTime.MinValue;
			var lastModifiedDateFromRequest = base.TryToGetIfModifiedSinceDateFromRequest();
			if ((lastModifiedDateFromRequest != null) && (Math.Abs(lastModifiedDateFromRequest.Value.Subtract(lastModifiedDateOfData).TotalSeconds) < 2))
			{
				// Add a small grace period to the comparison (if only because lastModifiedDateOfLiveData is granular to milliseconds while lastModifiedDate only
				// considers seconds and so will nearly always be between zero and one seconds older)
				Response.StatusCode = 304;
				Response.StatusDescription = "Not Modified";
				return Json(null, JsonRequestBehavior.AllowGet);
			}

			base.SetResponseCacheHeadersForSuccess(lastModifiedDateOfData);
			return Json(
				_postIndexer.GenerateIndexContent(posts).AutoCompleteContent,
				JsonRequestBehavior.AllowGet
			);
		}
	}
}
