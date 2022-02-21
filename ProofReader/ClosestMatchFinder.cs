using System;
using System.Collections.Generic;
using System.Linq;
using Fastenshtein;

namespace ProofReader
{
    internal static class ClosestMatchFinder
    {
        public static Func<string, string?> Get(
            IEnumerable<string> caseSensitiveKnownWords,
            IEnumerable<string> caseIrrelevantKnownWords,
            Func<string, int> wordCountInHistoricalData)
        {
            var caseSensitiveLookup = caseSensitiveKnownWords.ToHashSet(StringComparer.Ordinal);
            var caseIrrelevantLookup = caseIrrelevantKnownWords
                .Select(word => word.ToLowerInvariant()) // Note: The Levenshtein appplies greater distance to the same word with different casing
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return typo =>
            {
                // Check for a precise match first - no approximating required if one is found!
                if (caseSensitiveLookup.Contains(typo) || caseIrrelevantLookup.Contains(typo))
                    return typo;

                // Note: This will offer what it thinks is a best match but it will never be perfect when considering words in
                // isolation - more sentence context would be required to improve accuracy. However, it's good enough for fixing
                // up historical data with a manual review involved,
                var lev = new Levenshtein(typo.ToLowerInvariant()); // Note: Lower case to match justification above
                return caseIrrelevantLookup
                    .Select(word => (Word: word, Distance: lev.DistanceFrom(word), OccurencesInHistoricalData: wordCountInHistoricalData(word)))
                    .OrderBy(entry => entry.Distance)
                    .ThenByDescending(entry => entry.OccurencesInHistoricalData)
                    .FirstOrDefault()
                    .Word;
            };
        }
    }
}