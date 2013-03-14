using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BlogBackEnd.Models;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Core.Indexes.TernarySearchTree;
using FullTextIndexer.Core.IndexGenerators;
using FullTextIndexer.Core.TokenBreaking;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Common.Logging;

namespace BlogBackEnd.FullTextIndexing
{
	public class PostIndexer : IPostIndexer
	{
		/// <summary>
		/// This will never return null, it will throw an exception for null input.
		/// </summary>
		public PostIndexContent GenerateIndexContent(NonNullImmutableList<Post> posts)
		{
			if (posts == null)
				throw new ArgumentNullException("post");
			
			// The Search Index data uses an EnglishPluralityStringNormaliser which removes a lot of content from strings but the token set will never
			// be visible to a site user, they will pass a string into the index to match and only see the results.
			var whitespaceTokenBreaker = new WhiteSpaceExtendingTokenBreaker(
				new ImmutableList<char>(new[] {
					'<', '>', '[', ']', '(', ')', '{', '}',
					'.', ',', ':', ';', '"', '?', '!',
					'/', '\\',
					'@', '+', '|', '='
				}),
				new WhiteSpaceTokenBreaker()
			);
			var defaultIndexDataForSearching = GenerateIndexData(
				posts,
				new EnglishPluralityStringNormaliser(
					new DefaultStringNormaliser(),
					EnglishPluralityStringNormaliser.PreNormaliserWorkOptions.PreNormaliserLowerCases
					| EnglishPluralityStringNormaliser.PreNormaliserWorkOptions.PreNormaliserTrims
				),
				whitespaceTokenBreaker
			);

			// The AutoComplete content WILL be visible to the user and so we can't be as aggressive with the token normalisation. We'll start
			// off generating a token set in a similar manner as for the search index but instead of applying the EnglishPluralityStringNormaliser
			// and DefaultStringNormaliser we'll take the string unaltered and then filter out values that don't look like words that might need to
			// be searched on; those less than 3 characters, those that start with punctuation (eg. quoted values) or those that contains numbers.
			// Distinct values (ignoring case) will be taken and a final pass through the Search Index data will be done in case the difference in
			// token normalisation resulted in any AutoComplete words being produced that don't match anything when searched on (these are removed
			// from the results). The results are ordered alphabetically (again, ignoring case) to give the final content.
			var indexDataForAutoCompleteExtended = GenerateIndexData(
				posts,
				new NonAlteringStringNormaliser(),
				whitespaceTokenBreaker
			);
			var autoCompleteContent = new NonNullOrEmptyStringList(
				indexDataForAutoCompleteExtended.GetAllTokens()
				.Select(token => token.Trim())
				.Where(token =>(token.Length >= 3) && !char.IsPunctuation(token[0]) && !token.Any(c => char.IsNumber(c)))
				.Distinct(StringComparer.InvariantCultureIgnoreCase)
				.Where(token => defaultIndexDataForSearching.GetMatches(token).Any())
				.OrderBy(token => token.ToLower())
			);

			return new PostIndexContent(defaultIndexDataForSearching, autoCompleteContent);
		}

		private IIndexData<int> GenerateIndexData(NonNullImmutableList<Post> posts, IStringNormaliser sourceStringComparer, ITokenBreaker tokenBreaker)
		{
			if (posts == null)
				throw new ArgumentNullException("posts");
			if (sourceStringComparer == null)
				throw new ArgumentNullException("sourceStringComparer");
			if (tokenBreaker == null)
				throw new ArgumentNullException("tokenBreaker");

			var contentRetrievers = new List<ContentRetriever<Post, int>>();
			contentRetrievers.Add(new ContentRetriever<Post, int>(
				p => new PreBrokenContent<int>(p.Id, p.Title),
				GetTokenWeightDeterminer(5f, sourceStringComparer)
			));
			contentRetrievers.Add(new ContentRetriever<Post, int>(
				p => new PreBrokenContent<int>(p.Id, HttpUtility.HtmlDecode(p.MarkdownContent)),
				GetTokenWeightDeterminer(1f, sourceStringComparer)
			));
			contentRetrievers.Add(new ContentRetriever<Post, int>(
				p => !p.Tags.Any() ? null : new PreBrokenContent<int>(p.Id, string.Join(" ", p.Tags)),
				GetTokenWeightDeterminer(3f, sourceStringComparer)
			));

			// In common language, characters such as "." and "," indicate breaks in words (unlike "'" or "-" which are commonly part of words).
			// When generating an index from content that contains C# (or other similar languages) there are a raft of other characters which
			// need to be treated similarly.
			return new IndexGenerator<Post, int>(
				contentRetrievers.ToNonNullImmutableList(),
				new DefaultEqualityComparer<int>(),
				sourceStringComparer,
				tokenBreaker,
				weightedValues => weightedValues.Sum(),
				new NullLogger()
			).Generate(posts.ToNonNullImmutableList());
		}

		private ContentRetriever<Post, int>.BrokenTokenWeightDeterminer GetTokenWeightDeterminer(float multiplier, IStringNormaliser sourceStringComparer)
		{
			if (multiplier <= 0)
				throw new ArgumentOutOfRangeException("multiplier", "must be greater than zero");
			if (sourceStringComparer == null)
				throw new ArgumentNullException("sourceStringComparer");

			// Constructing a HashSet of the normalised versions of the stop words means that looking up whether normalised tokens are stop
			// words can be a lot faster (as neither the stop words nor the token need to be fed through the normaliser again)
			var hashSetOfNormalisedStopWords = new HashSet<string>(
				FullTextIndexer.Core.Constants.GetStopWords("en").Select(word => sourceStringComparer.GetNormalisedString(word))
			);
			return normalisedToken => multiplier * (hashSetOfNormalisedStopWords.Contains(normalisedToken) ? 0.01f : 1f);
		}

		[Serializable]
		private class NonAlteringStringNormaliser : StringNormaliser
		{
			public override string GetNormalisedString(string value)
			{
				if (string.IsNullOrWhiteSpace(value))
					throw new ArgumentException("Null/blank value specified");

				return value;
			}
		}
	}
}
