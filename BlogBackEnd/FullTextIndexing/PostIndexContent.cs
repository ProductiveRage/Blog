using System;
using System.Linq;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.TokenBreaking;

namespace BlogBackEnd.FullTextIndexing
{
	[Serializable]
	public class PostIndexContent
	{
		private readonly IIndexData<int> _searchIndex;
		public PostIndexContent(IIndexData<int> searchIndex, NonNullOrEmptyStringList autoCompleteContent)
		{
			if (searchIndex == null)
				throw new ArgumentNullException("searchIndex");
			if (autoCompleteContent == null)
				throw new ArgumentNullException("autoCompleteContent");

			_searchIndex = searchIndex;
			AutoCompleteContent = autoCompleteContent;
		}

		/// <summary>
		/// This will never return null. It will raise an exception for a null or blank term.
		/// </summary>
		public NonNullImmutableList<WeightedEntryWithTerm<int>> Search(string term)
		{
			if (string.IsNullOrWhiteSpace(term))
				throw new ArgumentException("Null/blank term specified");

			return _searchIndex.GetPartialMatches(
				term,
				new WhiteSpaceExtendingTokenBreaker(
					new ImmutableList<char>(new[] { '<', '>', '[', ']', '(', ')', '{', '}', '.', ',' }),
					new WhiteSpaceTokenBreaker()
				),
				(tokenMatches, allTokens) => (tokenMatches.Count < allTokens.Count) ? 0 : tokenMatches.SelectMany(m => m.Weights).Sum()
			);
		}

		/// <summary>
		/// This will never be null
		/// </summary>
		public NonNullOrEmptyStringList AutoCompleteContent { get; private set; }
	}
}
