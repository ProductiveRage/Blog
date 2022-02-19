using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FullTextIndexer.Common.Lists;

namespace ProofReader
{
    internal static class StringExtensions
    {
        public static string TrimNonLettersAndDigits(this string source)
        {
            // TODO: Could be much more efficient to look for start and end indexes rather than all this trimming
            while ((source.Length > 0) && !char.IsLetterOrDigit(source.First()))
                source = source[1..];
            while ((source.Length > 0) && !char.IsLetterOrDigit(source.Last()))
                source = source[0..^1];
            return source;
        }

        public static string RemoveDiacritics(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append(c);
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// This split a string into multiple words if it looks like the value represented multiple words, such as hyphen-connected-terms
        /// or PascalCasedTerms. It will split on any symbol except for an apostrophe (because that is often part of the word and not a
        /// separate work from the rest of the input string).
        /// </summary>
        public static IEnumerable<(string Token, char? Separator)> SplitIntoIndividualWords(this string source)
        {
            var buffer = "";
            foreach (var c in source)
            {
                if (!char.IsLetterOrDigit(c) && (c != '\''))
                {
                    yield return (buffer, c);
                    buffer = "";
                    continue;
                }

                if (char.IsUpper(c))
                {
                    if (buffer != "")
                        yield return (buffer, null);
                    buffer = c.ToString();
                    continue;
                }

                buffer += c;
            }
            if (buffer != "")
                yield return (buffer, null);
        }
    }
}