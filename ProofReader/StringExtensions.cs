using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FullTextIndexer.Common.Lists;

namespace ProofReader
{
    internal static class StringExtensions
    {
        public static string NormaliseLineEndingsWithoutAffectingCharacterIndexes(this string source) =>
            source
                .Replace("\r\n", " \n") // Note: Important to maintain character count (so include space before single line return)
                .Replace('\r', '\n');

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

        public static bool IsNumber(this string value)
        {
            if (value.All(c => (c == '.') || (c == '-') || (c == '+') || (c == 'π') || ((c >= '0') && (c <= '9'))))
            {
                // Approximate but good enough (allow ".12" or "1.2.2" - call the last one a version number)
                // - Originally intended for numbers but will probably suffice for common numeric ranges
                return true;
            }

            if (Regex.IsMatch(value, @"^\d+(st|nd|rd|th)$"))
            {
                // Again, approximate but will suffice - can live with "3th" instead of "3rd"
                return new[] { "st", "nd", "rd", "th" }.Contains(value[^2..], StringComparer.OrdinalIgnoreCase);
            }

            // Check for common units of measurement; "2.02s", "3ms", "10k", "128px", "5.2x"
            if (Regex.IsMatch(value, @"^\d+(\.\d+)?(s|ms|k|em|rem|px|x|kb|mb|mbps|gb|fps|m|am|pm|bit|bits)$", RegexOptions.IgnoreCase))
                return true;

            // Check for common dimensions (with or without pixel units included); "128x128", "3x3x3", "32x32px"
            if (Regex.IsMatch(value, @"^\d+x\d+(x\d+)?(px)?$"))
                return true;

            // Check for html hex code values (shortest is #RGB while longest is #RRGGBBAA)
            if (Regex.IsMatch(value, "#[0-9a-fA-F]{3,8}"))
                return true;

            return false;
        }

    }
}