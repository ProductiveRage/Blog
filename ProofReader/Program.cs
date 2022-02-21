using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Blog.Models;
using BlogBackEnd.Models;
using Fastenshtein;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.TokenBreaking;
using HtmlAgilityPack;

namespace ProofReader
{
    /// <summary>
    /// This was written when I realised how many spelling mistakes had slipped through the cracks over the years in my blog posts - the
    /// premise is to identify them and suggest corrections (potentially going as far as automatically correcting them in the future,
    /// pendin a manual review before committing).
    /// 
    /// Challenges include the large technical vocabulary (which has many words not found in common English dictionaries) and the content
    /// of code samples (for the purposes of this, code samples are skipped entirely).
    /// </summary>
    internal static class Program
    {
        // The words list leans into US English, so encourage it back to GB - it does have plenty of good coverage for UK for but it's
        // missing some particular words such as "endeavours" and I don't wish to be told that the correct spelling is not correct!
        private static readonly IEnumerable<(string From, string To)> _englishVariations =
            new[] { ("ize", "ise"), ("izing", "ising"), ("izable", "isable"), ("ization", "isation"), ("endeavor", "endeavour"), ("vorite", "vourite") };

        private static async Task Main(string[] args)
        {
            var postFolderPath = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(postFolderPath))
                throw new Exception("No folder path specified in arguments to locate blog posts");

            if (!Directory.Exists(postFolderPath))
                throw new Exception("Specified folder path does not exist: " + postFolderPath);

            // Prepare lookups of common English words
            Console.WriteLine("Reading English dictionary for typo detection..");
            var caseSensitiveKnownWords = new HashSet<string>(new WordListDataStringEqualityComparer(caseSensitive: true));
            var caseIrrelevantKnownWords = new HashSet<string>(new WordListDataStringEqualityComparer(caseSensitive: false));
            var wordsList = Unzip(await File.ReadAllBytesAsync("words.txt.gz"))
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line));
            foreach (var word in wordsList)
            {
                // Skip over any entries that looks like they indicate partial words (eg. "pre-") or abbreviations like "eg." because it
                // introduces too much potential for confusion in processing (and common abbreviations like "eg." should get picked up
                // by the pass through historical blog post data further down)
                if (word.TrimNonLettersAndDigits() != word)
                    continue;

                // The dictionary contains entries that are all lower case that seem to be appropriate for use as full standard words,
                // regardless of their casing - however, there are some (such as "IBM", "ICAN", "POSI") that are all capitals and are
                // acronyms that shouldn't be used as acceptable spellings - eg. a misspelling of "pose" as "pos" shouldn't be allowed
                // to slide just because there is a "POS" entry (which could mean Point Of Sales).
                // - This logic is only applied to words that are ALL captials because words that this wouldn't seem to apply to (such
                //   as "Brink" have a capital "B"" in the wod list)
                if (word.All(char.IsUpper))
                {
                    caseSensitiveKnownWords.Add(word);
                    continue;
                }

                // Note: NOT including the original version because I want to consider American spellings mistakes on my blog
                foreach (var (from, to) in _englishVariations)
                    AddWordToCaseIrrelevantList(word.Replace(from, to), caseIrrelevantKnownWords);

                static void AddWordToCaseIrrelevantList(string word, HashSet<string> caseIrrelevantKnownWords)
                {
                    // To make things worse, some words that AREN'T acronyms are all capitals - such as "RACE" (there is no lower-case version
                    // to use as the verb "to race"). As a further workaround, for the case-insensitive-matching words, the original value will
                    // be added but if it contains any hyphens then it will be broken down and each individual word also added (and so "race"
                    // will be added from entries such as "race-winning" and "aids" will be added from "aids-de-camp")
                    caseIrrelevantKnownWords.Add(word);
                    if (word.Contains('-'))
                    {
                        foreach (var partial in word.Split('-').Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w)))
                            caseIrrelevantKnownWords.Add(partial);
                    }
                }
            }

            // Add in additional terms to the English word lookups by parsing historical blog posts - due to the nature of the articles,
            // there are likely to be many technical terms that are not known to common English dictionaries but which are not incorrect.
            // Hopefully this step will reduce false positives.
            // - Note that Markdig, HtmlAgilityPack and a simple WhiteSpaceExtendingTokenBreaker are all used here because we only care
            //   about extending the lexicon; we don't care where, specifically, in blog posts that terms came from (when looking for
            //   spelling mistakes further down, the parsing will be done differently such that the source location of each token is
            //   maintained in case an automated apply-best-suggested-correction step is to be added)
            Console.WriteLine("Looking for additional common terms from Posts..");
            var breakOn = new ImmutableList<char>(
                '<', '>', '(', ')', '{', '}', // Don't break on square brackets due to post content like "cact[us][ii]'
                ',', ':', ';', '"', '?', '!', // Don't break on "." so that it's easier to support file extensions or abbreviations like "pp."
                '/', '\\',
                '@', '+', '|', '=');
            var plainTextTokenBreaker = new WhiteSpaceExtendingTokenBreaker(breakOn, new WhiteSpaceTokenBreaker());
            var tokensFromPosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var postRetriever = new SingleFolderPostRetriever(new DirectoryContents(postFolderPath));
            var posts = await postRetriever.Get();
            foreach (var post in posts)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(MarkdownTransformations.ToHtml(post.MarkdownContent));

                // Remove code blocks because they are a much lower proportion of English
                var preNodes = doc.DocumentNode.SelectNodes("//code");
                if (preNodes is not null)
                {
                    foreach (var preNode in preNodes)
                    {
                        preNode.Remove();
                    }
                }

                // Use DeEntitize as per https://html-agility-pack.net/knowledge-base/21842886
                var plainText = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
                foreach (var token in plainTextTokenBreaker.Break(plainText))
                {
                    var tokenValue = token.Token.TrimNonLettersAndDigits();
                    if (tokenValue.Length > 1)
                    {
                        if (!tokensFromPosts.ContainsKey(tokenValue))
                            tokensFromPosts.Add(tokenValue, 0);
                        tokensFromPosts[tokenValue]++;
                    }
                }
            }

            // Try to find a balance - hopefully low enough so that correct uncommon technical terms are identified but not words
            // that I've spelt incorrectly too often!
            const int minFrequencyOfAppearanceInPostsToConsider = 3;
            foreach (var (termFromPosts, _) in tokensFromPosts.Where(kvp => kvp.Value >= minFrequencyOfAppearanceInPostsToConsider))
            {
                // These are added to the case-insensitive matching list because they aren't being read from a curated list that
                // may have special rules and expectations about casing
                caseIrrelevantKnownWords.Add(termFromPosts);
            }

            Console.WriteLine("Reading custom word list for site..");
            foreach (var word in (await File.ReadAllLinesAsync("Custom Additions.txt")).Select(line => line.Trim()))
            {
                if (string.IsNullOrWhiteSpace(word))
                    continue;

                // Allow the Custom Additions file to be condensed slightly for similar words where only the ending differs - eg.
                // instead of separate entries for "flatlined" and "flatlining", allow a single entry "flatlin/ed/ing" that will
                // be split on the slashes and the first segment combined with each of the following segments to recreate the
                // original variations of the word.
                IEnumerable<string> variations;
                if (word.Contains('/'))
                {
                    var segments = word.Split('/');
                    variations = segments.Skip(1).Select(ending => segments.First() + ending);
                }
                else
                {
                    variations = new[] { word };
                }

                // Any terms that are entirely lower case are presumed to be usable in case-insensitive matches while any terms
                // that have at least one upper case character as presumed to be case SENSITIVE (as with the example earlier, it
                // may be desirable to specify "PoS" to mean Point of Sales but that shouldn't mean that a lower-cased misspelling
                // "pos" of "pose" should be overlooked)
                var hashSetToAddTo = word.Any(char.IsUpper) ? caseSensitiveKnownWords : caseIrrelevantKnownWords;
                foreach (var variation in variations)
                    hashSetToAddTo.Add(variation);
            }

            // Use the common word lookup lists to generate a "select closest match" method using the Levenshtein distance method -
            // when a word is encountered that is thought to be misspelt then this may be used to suggest a similar but correct
            // alternative
            Console.WriteLine("Reading English dictionary for generating suggestions..");
            var closestMatchFinder = GetClosestMatchFinder(
                caseSensitiveKnownWords,
                caseIrrelevantKnownWords,
                suggestion =>
                    tokensFromPosts.TryGetValue(suggestion, out var count)
                        ? count
                        : 0);

            // Read each post and apply a limited form of Markdown transformation such that each word maintains its position in the
            // the original text - eg. remove code sections because I want to concentrate on finding spelling mistakes in English
            // prose but the code sections are replaced with empty lines so that the positions of text following it is unaffected.
            // This allows for the any word that is thought to be spelt incorrectly to have its location reported as it would be
            // in the original Markdown text. The downside is that it limits what Markdown transformations are supported but it
            // is sufficient for my case.
            var possibleHtmlEntityContainingContentTokenBreaker = new HtmlEncodedEntityTokenBreaker(breakOn);
            foreach (var post in posts)
            {
                var haveRenderedPostName = false;

                // Note: Taking this manual approach instead of rendering from markdown to plain text to try to keep the original source
                // locations of the tokens in case want to explore any automated replacements
                var postContent = post.MarkdownContent
                    .RemoveCodeBlocks()
                    .RemoveLinkUrls()
                    .RemoveExplicitImgTags()
                    .RemoveExplicitLineBreaks();

                // Note: Using the HtmlEncodedEntityTokenBreaker means that any decoding of html entities is already done - so each
                // token.Content is a plain string (if triangular brackets were encoded and used as token separators then they will
                // not appear as tokens but if there was a copyright symbol, encoded as "&copy;", for example, then the token content
                // would include the copyright symbol itself)
                var tokens = possibleHtmlEntityContainingContentTokenBreaker.Break(postContent);
                foreach (var token in tokens)
                {
                    if (IsGoodWord(token.Token, IsInKnownWordLists))
                        continue;

                    var partialTokenSuggestions = token.Token
                        .SplitIntoIndividualWords()
                        .Select(partial =>
                        {
                            // For tokens that are actually multiple combined tokens (eg. "my-list-of-items"), they will be split up
                            // and each individual token checked for spelling and an alternative value offered where appropriate and
                            // possible.
                            var suggestion = partial.Token != ""
                                ? closestMatchFinder(partial.Token)
                                : "";
                            if (suggestion is null)
                                suggestion = "???";
                            else if ((suggestion.Length > 0) && (partial.Token.Length > 0))
                            {
                                // Suggested words are lower case but if they are being suggested for a word with a capital first
                                // letter then that will be maintained on the suggestion (eg. ensuring "MyApplicaton" is split into
                                // "My" and "Applicaton", the second word corrected to "application" and its casing then restored to
                                // "Application" so that the final recombined term is "MyApplication")
                                if (char.IsUpper(partial.Token[0]))
                                    suggestion = char.ToUpperInvariant(suggestion[0]) + suggestion[1..];
                                else if (char.IsLower(partial.Token[0]))
                                    suggestion = char.ToLowerInvariant(suggestion[0]) + suggestion[1..];
                            }
                            return suggestion + (partial.Separator is null ? "" : partial.Separator.ToString());
                        });
                    var suggestedReplacement = string.Join("", partialTokenSuggestions);
                    if (suggestedReplacement == token.Token)
                    {
                        // This happens with "w3wp.exe" due to the discrepancies in not splitting on "." when parsing the initial
                        // content but supporting it will trying to break a token down to see if its individual components make
                        // it valid - that's fine, so let's just move on
                        continue;
                    }

                    if (!haveRenderedPostName)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Reading post {post.Id} {post.Title}..");
                        haveRenderedPostName = true;
                    }

                    Console.Write($"Bad word {GetRowAndColumnSourceIndex(postContent, token.SourceLocation.SourceIndex)}: {token.Token}");
                    Console.Write(" => " + suggestedReplacement);
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done! Press [Enter] to terminate..");
            Console.ReadLine();

            bool IsInKnownWordLists(string value) =>
                caseSensitiveKnownWords.Contains(value) || caseIrrelevantKnownWords.Contains(value);
        }

        private static bool IsGoodWord(string source, Func<string, bool> knownWordLookup)
        {
            // Note: If this term is a single word OR it's a number / unit of measurement then we don't need to
            // do any more work and can just drop out now
            if (knownWordLookup(source) || IsNumber(source))
                return true;

            // Some strings contain multiple words that are combined (eg. "my-application") but it may be necessary
            // to try different ways of separating them; sometimes splitting on anything that isn't a number or a
            // digit or an apostrophe works while sometimes it's necessary to also split whenever a capital letter
            // is encountered (though this can prove problematic for strings such as "this-should-be-UTF8")
            foreach (var newTokenWhenCapitalLetterEncountered in new[] { false, true })
            {
                var subTokens = new List<string>();
                var buffer = "";
                foreach (var c in source)
                {
                    // An example of splitting on symbols apart from apostrophes is the term:
                    //
                    //   "sometimes-Key-is-null-and-sometimes-it-isn't"
                    //
                    //.. because it's important to keep the apostrophe as part of the word "isn't"
                    if (!char.IsLetterOrDigit(c) && (c != '\''))
                    {
                        AddBufferToListIfNonEmpty();
                        buffer = "";
                        continue;
                    }

                    // Trying this approach with newTokenWhenCapitalLetterEncountered both as false and true has some
                    // advantages and disadvantages for different strings - eg. "ConsoleApplication1" will be split
                    // correctly if newTokenWhenCapitalLetterEncountered is true while "spot-the-WTFs" will not.
                    var isCasingBreak = (newTokenWhenCapitalLetterEncountered && char.IsUpper(c)) || char.IsNumber(c);
                    if (isCasingBreak)
                    {
                        AddBufferToListIfNonEmpty();
                        buffer = c.ToString(); // Note: We're keeping this character because it's part of the new word, not just a separator
                        continue;
                    }

                    buffer += c;
                }
                AddBufferToListIfNonEmpty();
                if (!subTokens.Any())
                    return true;

                // If the term has been broken down into multiple smaller terms such that ALL of them are either recognised words
                // or numbers / units of measurement then we're done 
                if (subTokens.All(t => IsNumber(t) || knownWordLookup(t)))
                    return true;

                void AddBufferToListIfNonEmpty()
                {
                    // Each time that we encounter the end of a sub term, we need to empty the buffer and add its content (if any)
                    // to the subTokens list. However, there will be some strings that consist only of apostrophes since we are
                    // never excluding them when populating the buffer and so we want to trim them away before considering recording
                    // the sub token (in cases where a sub token correctly has a possessive trailing apostrophe, it will not make any
                    // difference to the spelling checking logic to remove it here)
                    buffer = buffer.Trim('\'');
                    if (buffer != "")
                        subTokens.Add(buffer);
                }
            }

            // We might be able to easily expand the known vocabulary by prepending with "de" or "re" (eg. recognising "deserialising"
            // because we're aware of the existence of "serialising" or "recategorised" because we're aware of "categorised")
            source = source.TrimNonLettersAndDigits();
            if (source.StartsWith("de", StringComparison.OrdinalIgnoreCase) || source.StartsWith("re", StringComparison.OrdinalIgnoreCase))
            {
                if (knownWordLookup(source[2..]))
                    return true;
            }

            return false;
        }

        static string Unzip(byte[] compressed) // Courtesy of https://stackoverflow.com/a/7343623/3813189
        {
            using var compressedData = new MemoryStream(compressed);
            using var decompressedData = new MemoryStream();
            {
                using (var gs = new GZipStream(compressedData, CompressionMode.Decompress))
                {
                    gs.CopyTo(decompressedData);
                }
                return Encoding.UTF8.GetString(decompressedData.ToArray());
            }
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
            foreach (var line in NormaliseLineEndingsWithoutAffectingCharacterIndexes(source).Split('\n'))
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

        static (int Column, int Row) GetRowAndColumnSourceIndex(string source, int sourceIndex)
        {
            var rowIndex = 0;
            var colIndex = 0;
            foreach (var c in NormaliseLineEndingsWithoutAffectingCharacterIndexes(source).Take(sourceIndex))
            {
                if (c == '\n')
                {
                    rowIndex++;
                    colIndex = 0;
                    continue;
                }
                colIndex++;
            }
            return (colIndex + 1, rowIndex + 1);
        }

        static bool IsNumber(string value)
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

        private static string NormaliseLineEndingsWithoutAffectingCharacterIndexes(string source) =>
            source
                .Replace("\r\n", " \n") // Note: Important to maintain character count (so include space before single line return)
                .Replace('\r', '\n');

        private static Func<string, string?> GetClosestMatchFinder(
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