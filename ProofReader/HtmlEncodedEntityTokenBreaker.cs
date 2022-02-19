using System.Collections.Generic;
using System.Linq;
using System.Web;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.Indexes;
using FullTextIndexer.Core.TokenBreaking;

namespace ProofReader
{
    /// <summary>
    /// A variation of a split-on-whitespace-and-other-specified-characters token breaker that can deal with content that may have
    /// some encoded html entities in it (triangular brackets, for example) - these entities may still be used as characters to
    /// delineate tokens and the source location data of the generated tokens will match the original string (in case any
    /// replacements wish to be made, which would not be possible if the entire input string was HTML-decoded before breaking).
    /// 
    /// Note that not only may html-encoded entities be used to delineate tokens, any encoded entities that are not used as
    /// separators will appear in the returned Token Content strings as the decoded characters and so no further decoding is
    /// required to use the results from this class.
    /// </summary>
    internal sealed class HtmlEncodedEntityTokenBreaker : ITokenBreaker
    {
        private readonly ImmutableList<char> _breakOnAsWellAsWhitespace;
        public HtmlEncodedEntityTokenBreaker(ImmutableList<char> breakOnAsWellAsWhitespace) =>
            _breakOnAsWellAsWhitespace = breakOnAsWellAsWhitespace;

        public NonNullImmutableList<WeightAdjustingToken> Break(string value)
        {
            var buffer = (StartIndex: 0, Length: 0, Content: "");
            var tokens = new List<(int TokenIndex, int SourceIndex, int SourceTokenLength, string Content)>();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (char.IsWhiteSpace(c) || _breakOnAsWellAsWhitespace.Contains(c))
                {
                    if (buffer.Content != "")
                        tokens.Add((tokens.Count, buffer.StartIndex, i - buffer.StartIndex, buffer.Content));
                    buffer = (StartIndex: i + 1, Length: 0, Content: "");
                    continue;
                }

                if (c == '&')
                {
                    var possibleEntityBuffer = c.ToString();
                    foreach (var c2 in value[(i + 1)..])
                    {
                        if (char.IsWhiteSpace(c2))
                            break;
                        possibleEntityBuffer += c2;
                        if (c2 == ';')
                            break;
                    }

                    // We only treat things differently if this IS an html-encoded character - otherwise we skip over the
                    // following conditional block and treat it like any other individual character
                    var decodedEntity = HttpUtility.HtmlDecode(possibleEntityBuffer);
                    if (decodedEntity != possibleEntityBuffer)
                    {
                        if ((decodedEntity.Length == 1) && (char.IsWhiteSpace(decodedEntity[0]) || _breakOnAsWellAsWhitespace.Contains(decodedEntity[0])))
                        {
                            if (buffer.Content != "")
                                tokens.Add((tokens.Count, buffer.StartIndex, i - buffer.StartIndex, buffer.Content));
                            buffer = (StartIndex: i + possibleEntityBuffer.Length, Length: 0, Content: "");
                        }
                        else
                        {
                            buffer.Content += decodedEntity;
                            buffer.Length += possibleEntityBuffer.Length;
                        }
                        i += possibleEntityBuffer.Length - 1;
                        continue;
                    }
                }

                buffer.Content += c;
                buffer.Length++;
            }
            if (buffer.Content != "")
            {
                tokens.Add((tokens.Count, buffer.StartIndex, value.Length - buffer.StartIndex, buffer.Content));
            }
            return tokens
                .Select(token => new WeightAdjustingToken(
                    token.Content,
                    weightMultiplier: 1,
                    new SourceLocation(token.TokenIndex, token.SourceIndex, token.SourceTokenLength)))
                .ToNonNullImmutableList();
        }
    }
}
