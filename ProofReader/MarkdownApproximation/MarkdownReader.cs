using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.TokenBreaking;
using HtmlAgilityPack;

namespace ProofReader.MarkdownApproximation
{
    /// <summary>
    /// Read each post and apply a limited form of Markdown transformation such that each word maintains its position in the
    /// the original text - eg. remove code sections because I want to concentrate on finding spelling mistakes in English
    /// prose but the code sections are replaced with empty lines so that the positions of text following it is unaffected.
    /// This allows for the any word that is thought to be spelt incorrectly to have its location reported as it would be
    /// in the original Markdown text. The downside is that it limits what Markdown transformations are supported but it
    /// is sufficient for my case.
    /// </summary>
    internal static class MarkdownReader
    {
        public static NonNullImmutableList<WeightAdjustingToken> GetTokens(string markdownContent, ImmutableList<char> breakOn)
        {
            if (markdownContent == null)
                throw new ArgumentNullException(nameof(markdownContent));
            if (breakOn == null)
                throw new ArgumentNullException(nameof(breakOn));

            // Note: Taking this manual approach instead of rendering from markdown to plain text to try to keep the original source
            // locations of the tokens in case want to explore any automated replacements
            var postContent = markdownContent
                .RemoveCodeBlocks()
                .RemoveLinkUrls()
                .RemoveExplicitImgTags()
                .RemoveExplicitLineBreaks();

            // Note: Using the HtmlEncodedEntityTokenBreaker means that any decoding of html entities is already done - so each
            // token.Content is a plain string (if triangular brackets were encoded and used as token separators then they will
            // not appear as tokens but if there was a copyright symbol, encoded as "&copy;", for example, then the token content
            // would include the copyright symbol itself)
            return new HtmlEncodedEntityTokenBreaker(breakOn).Break(postContent);
        }

        private static string RemoveCodeBlocks(this string source)
        {
            // Remove multiline code blocks that are indicated by three backticks before and after content
            source = Regex.Replace(
                source,
                "```(.*?)```",
                match => new string(match.Value.Select(c => char.IsWhiteSpace(c) ? c : ' ').ToArray()),
                RegexOptions.Singleline // Treat "." to match EVERY character (not just every one EXCEPT new lines)
            );

            // Remove multiline code blocks that are indicated by four trailing spaces per line
            var content = new StringBuilder();
            foreach (var line in source.NormaliseLineEndingsWithoutAffectingCharacterIndexes().Split('\n'))
            {
                var contentForLine = line.StartsWith("    ")
                    ? new string(' ', line.Length)
                    : line;
                content.Append(contentForLine + '\n');
            }

            // Remove inline code blocks (indicated by single backticks before and after the content)
            return Regex.Replace(
                content.ToString(),
                "`(.*?)`",
                match => new string(' ', match.Length)
            );
        }

        private static string RemoveLinkUrls(this string source) =>
            Regex.Replace(
                source,
                @"\[([^\]]*?)(\[.*?\])?\]\((.*?)\)",
                match =>
                {
                    // General link syntax is of the form [description](http://example.com) and we want to spell check the "description" string
                    // while replacing the rest of the content with whitespace so that the token positions don't change. There are a couple
                    // of exceptions to this, such as where the text is either the same as the url (eg. "http://example.com") or the same
                    // but without the protocol to make it shorter (eg. "example.com") - in both of these cases, we want to replace the
                    // description with whitespace as well. Finally, SOMETIMES the description includes a format note in its content in
                    // square brackets (eg. [description [PDF]](http://example.com/doc.pdf)) and we need a separate capture group in
                    // the regex to pick up on that (and include it in the linkText value if this optional text is present).
                    var linkText = match.Groups[1].Value;
                    if (match.Groups[2].Success)
                        linkText += " " + match.Groups[2].Value;
                    if (linkText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || linkText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        // If there is no text description for the link (it's just the URL) then replace the entire content with whitespace
                        return new string(' ', match.Length);
                    }
                    var linkUrl = match.Groups[3].Value;
                    if (linkUrl.Contains("://") && (linkText == linkUrl.Split("://", 2).Last()))
                    {
                        // This is basically the same case as above except that the protocol has been hidden from the link text
                        return new string(' ', match.Length);
                    }
                    var textToKeep = match.Groups[1].Value;
                    textToKeep = " " + textToKeep; // Prepend a space to replace the opening square bracket that was removed
                    return textToKeep + new string(' ', match.Length - textToKeep.Length); // Pad out the rest with spaces to maintain total length
                });

        private static string RemoveExplicitImgTags(this string source) =>
            Regex.Replace(
                source,
                @"<img.*?\/>",
                match =>
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(match.Value);
                    var altText = doc.DocumentNode.ChildNodes.FirstOrDefault()?.Attributes["alt"]?.DeEntitizeValue ?? "";
                    return altText + new string(' ', match.Length - altText.Length);
                });

        private static string RemoveExplicitLineBreaks(this string source) =>
            Regex.Replace(
                source,
                @"<br.*?\/>",
                match => new string(' ', match.Length));
    }
}