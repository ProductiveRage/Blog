using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ProofReader
{
    internal sealed class WordListDataStringEqualityComparer : IEqualityComparer<string>
    {
        private readonly StringComparison _comparison;
        public WordListDataStringEqualityComparer(bool caseSensitive) =>
            _comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

        public bool Equals(string? x, string? y)
        {
            if ((x is null) && (y is null))
                return true;
            if ((x is null) || (y is null))
                return false;
            return Normalise(x).Equals(Normalise(y), _comparison);
        }

        public int GetHashCode([DisallowNull] string obj) => Normalise(obj).GetHashCode(_comparison);

        private static string Normalise(string value)
        {
            if (value.EndsWith("\'s", StringComparison.OrdinalIgnoreCase))
                value = value[0..^2];

            return value.RemoveDiacritics();
        }
    }
}