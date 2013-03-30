using System;
using System.Collections.Generic;
using System.Linq;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;

namespace BlogBackEnd.FullTextIndexing
{
	public static class SearchTermHighlighter
	{
		/// <summary>
		/// TODO (inc. non-overlapping of returned segments)
		/// </summary>
		public static NonNullImmutableList<StringSegment> IdentifySearchTermsToHighlight(
			string content,
			int maxLengthForHighlightedContent,
			NonNullImmutableList<SourceFieldLocation> sourceLocations,
			IComparer<NonNullImmutableList<SourceFieldLocation>> bestMatchDeterminer)
		{
			if (content == null)
				throw new ArgumentNullException("content");
			if (maxLengthForHighlightedContent <= 0)
				throw new ArgumentOutOfRangeException("maxLengthForHighlightedContent", "must be greater than zero");
			if (sourceLocations == null)
				throw new ArgumentNullException("sourceLocations");
			if (!sourceLocations.Any())
				throw new ArgumentException("There must be at least one source location specified");
			if (sourceLocations.Select(s => s.SourceFieldIndex).Distinct().Count() > 1)
				throw new ArgumentException("All sourceLocations must have the same SourceFieldIndex");
			if (bestMatchDeterminer == null)
				throw new ArgumentNullException("bestMatchDeterminer");

			// Sort sourceLocations by index and then length
			sourceLocations = sourceLocations.Sort((x, y) =>
			{
				if (x.SourceIndex < y.SourceIndex)
					return -1;
				else if (y.SourceIndex < x.SourceIndex)
					return 1;

				if (x.SourceTokenLength < y.SourceTokenLength)
					return -1;
				else if (y.SourceTokenLength < x.SourceTokenLength)
					return 1;

				return 0;
			});

			// Identify all combinations of source locations that can be shown at once without exceeding the maxLengthForHighlightedContent restraint
			var sourceLocationChains = new NonNullImmutableList<NonNullImmutableList<SourceFieldLocation>>();
			for (var indexOfFirstSourceLocationInChain = 0; indexOfFirstSourceLocationInChain < sourceLocations.Count; indexOfFirstSourceLocationInChain++)
			{
				var sourceLocationChain = new NonNullImmutableList<SourceFieldLocation>();
				for (var indexOfLastSourceLocationInChain = indexOfFirstSourceLocationInChain; indexOfLastSourceLocationInChain < sourceLocations.Count; indexOfLastSourceLocationInChain++)
				{
					var startPoint = sourceLocations[indexOfFirstSourceLocationInChain].SourceIndex;
					var endPoint = sourceLocations[indexOfLastSourceLocationInChain].SourceIndex + sourceLocations[indexOfLastSourceLocationInChain].SourceTokenLength;
					if ((endPoint - startPoint) > maxLengthForHighlightedContent)
						break;

					sourceLocationChain = sourceLocationChain.Add(sourceLocations[indexOfLastSourceLocationInChain]);
					sourceLocationChains = sourceLocationChains.Add(sourceLocationChain);
				}
			}

			// Get the best source location chain, if any (if not, return an empty set) and translate into a StringSegment set
			if (!sourceLocationChains.Any())
				return new NonNullImmutableList<StringSegment>();
			return ToStringSegments(
				sourceLocationChains.Sort(bestMatchDeterminer).First()
			);
		}

		/// <summary>
		/// TODO
		/// </summary>
		public static NonNullImmutableList<StringSegment> IdentifySearchTermsToHighlight(
			string content,
			int maxLengthForHighlightedContent,
			NonNullImmutableList<IndexData_Extensions_PartialMatches.SourceFieldLocationWithTerm> sourceLocations,
			IComparer<NonNullImmutableList<SourceFieldLocation>> bestMatchDeterminer)
		{
			if (sourceLocations == null)
				throw new ArgumentNullException("sourceLocations");

			return IdentifySearchTermsToHighlight(
				content,
				maxLengthForHighlightedContent,
				sourceLocations
					.Select(s => new SourceFieldLocation(s.SourceFieldIndex, s.TokenIndex, s.SourceIndex, s.SourceTokenLength, s.MatchWeightContribution))
					.ToNonNullImmutableList(),
				bestMatchDeterminer
			);
		}

		/// <summary>
		/// This will translate the source location data into StringSegment instances, combining any overlapping source locations (if none of them overlap then
		/// there will be the same number of StringSegments are there were SourceFieldLocations, if any do overlap then less StringSegments will be returned)
		/// </summary>
		private static NonNullImmutableList<StringSegment> ToStringSegments(NonNullImmutableList<SourceFieldLocation> sourceLocations)
		{
			if (sourceLocations == null)
				throw new ArgumentNullException("sourceLocations");
			if (!sourceLocations.Any())
				throw new ArgumentException("must not be empty", "sourceLocations");

			var stringSegments = new NonNullImmutableList<StringSegment>();
			var sourceLocationsToCombine = new NonNullImmutableList<SourceFieldLocation>();
			foreach (var sourceLocation in sourceLocations.Sort((x, y) => x.SourceIndex.CompareTo(y.SourceIndex)))
			{
				// If the current sourceLocation overlaps with the previous one (or adjoins it) then they should be combined together (if there
				// isn't a previous sourceLocation then start a new queue)
				if (!sourceLocationsToCombine.Any() || (sourceLocation.SourceIndex <= sourceLocationsToCombine.Max(s => (s.SourceIndex + s.SourceTokenLength))))
				{
					sourceLocationsToCombine = sourceLocationsToCombine.Add(sourceLocation);
					continue;
				}

				// If the current sourceLocation marks the start of a new to-highlight segment then add any queued-up sourceLocationsToCombine
				// content to the stringSegments set..
				if (sourceLocationsToCombine.Any())
					stringSegments = stringSegments.Add(new StringSegment(sourceLocationsToCombine));

				// .. and start a new sourceLocationsToCombine list
				sourceLocationsToCombine = new NonNullImmutableList<SourceFieldLocation>(new[] { sourceLocation });
			}
			if (sourceLocationsToCombine.Any())
				stringSegments = stringSegments.Add(new StringSegment(sourceLocationsToCombine));
			return stringSegments;
		}

		public class StringSegment
		{
			public StringSegment(NonNullImmutableList<SourceFieldLocation> sourceLocations)
			{
				if (sourceLocations == null)
					throw new ArgumentNullException("sourceLocations");
				if (!sourceLocations.Any())
					throw new ArgumentException("must not be empty", "sourceLocations");
				if (sourceLocations.Select(s => s.SourceFieldIndex).Distinct().Count() > 1)
					throw new ArgumentException("All sourceLocations must have the same SourceFieldIndex");

				Index = sourceLocations.Min(s => s.SourceIndex);
				Length = sourceLocations.Max(s => (s.SourceIndex + s.SourceTokenLength) - Index);
				SourceLocations = sourceLocations;
			}

			/// <summary>
			/// This will always be zero or greater
			/// </summary>
			public int Index { get; private set; }

			/// <summary>
			/// This will always be greater than zero
			/// </summary>
			public int Length { get; private set; }
			
			/// <summary>
			/// This will never be null or empty
			/// </summary>
			public NonNullImmutableList<SourceFieldLocation> SourceLocations { get; private set; }
		}
	}
}
