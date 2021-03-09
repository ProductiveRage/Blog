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
		/// Given a content string and a set of source locations all indicate matches within that content, return a set of string segments (each having an Index and Length
		/// that refer to the specified content) that should be highlighted if only a section of that content string is to rendered to represent a "search match summary".
		/// The length of the search match string is restricted will be no greater than the provided maxLengthForHighlightedContent value. For cases where multiple sets
		/// of string segments may be generated, the best match (according to the specified bestMatchDeterminer) will be returned. The returned string segments will never
		/// contain instances that overlap. If it was not possible to identify any segments to highlight (if the sourceLocations set was empty or if none of the matches
		/// could be highlighted without exceeding the maxLengthForHighlightedContent constraint), then an empty set will be returned. This will never return null.
		/// </summary>
		public static NonNullImmutableList<StringSegment> IdentifySearchTermsToHighlight(
			string content,
			int maxLengthForHighlightedContent,
			NonNullImmutableList<SourceFieldLocation> sourceLocations,
			IComparer<NonNullImmutableList<SourceFieldLocation>> bestMatchDeterminer)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));
			if (maxLengthForHighlightedContent <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxLengthForHighlightedContent), "must be greater than zero");
			if (sourceLocations == null)
				throw new ArgumentNullException(nameof(sourceLocations));
			if (sourceLocations.Select(s => s.SourceFieldIndex).Distinct().Count() > 1)
				throw new ArgumentException("All sourceLocations must have the same SourceFieldIndex");
			if (bestMatchDeterminer == null)
				throw new ArgumentNullException(nameof(bestMatchDeterminer));

			// If there are no source locations there there is nothing to highlight
			if (!sourceLocations.Any())
				return NonNullImmutableList<StringSegment>.Empty;

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
			var sourceLocationChains = NonNullImmutableList<NonNullImmutableList<SourceFieldLocation>>.Empty;
			for (var indexOfFirstSourceLocationInChain = 0; indexOfFirstSourceLocationInChain < sourceLocations.Count; indexOfFirstSourceLocationInChain++)
			{
				var sourceLocationChain = NonNullImmutableList<SourceFieldLocation>.Empty;
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
				return NonNullImmutableList<StringSegment>.Empty;
			return ToStringSegments(
				sourceLocationChains.Sort(bestMatchDeterminer).First()
			);
		}

		/// <summary>
		/// This will translate the source location data into StringSegment instances, combining any overlapping source locations (if none of them overlap then
		/// there will be the same number of StringSegments are there were SourceFieldLocations, if any do overlap then less StringSegments will be returned)
		/// </summary>
		private static NonNullImmutableList<StringSegment> ToStringSegments(NonNullImmutableList<SourceFieldLocation> sourceLocations)
		{
			if (sourceLocations == null)
				throw new ArgumentNullException(nameof(sourceLocations));
			if (!sourceLocations.Any())
				throw new ArgumentException("must not be empty", nameof(sourceLocations));

			var stringSegments = NonNullImmutableList<StringSegment>.Empty;
			var sourceLocationsToCombine = NonNullImmutableList<SourceFieldLocation>.Empty;
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

		public sealed class StringSegment
		{
			public StringSegment(NonNullImmutableList<SourceFieldLocation> sourceLocations)
			{
				if (sourceLocations == null)
					throw new ArgumentNullException(nameof(sourceLocations));
				if (!sourceLocations.Any())
					throw new ArgumentException("must not be empty", nameof(sourceLocations));
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
