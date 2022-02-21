using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blog.Models;
using BlogBackEnd.Models;
using FullTextIndexer.Common.Lists;
using FullTextIndexer.Core.TokenBreaking;
using HtmlAgilityPack;
using ProofReader.MarkdownApproximation;

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
            var postRetriever = new SingleFolderPostRetriever(
                new DirectoryInfo(postFolderPath)
                    .EnumerateFiles()
                    .Select(f => new FileProvidersFile(f)));
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
            var closestMatchFinder = ClosestMatchFinder.Get(
                caseSensitiveKnownWords,
                caseIrrelevantKnownWords,
                suggestion =>
                    tokensFromPosts.TryGetValue(suggestion, out var count)
                        ? count
                        : 0);

            foreach (var post in posts)
            {
                var tokens = MarkdownReader.GetTokens(post.MarkdownContent, breakOn);
                var suggestions = new List<(WeightAdjustingToken Token, string SuggestedReplacement)>();
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

                    suggestions.Add((token, suggestedReplacement));
                }
                if (!suggestions.Any())
                    continue;

                Console.WriteLine();
                Console.WriteLine($"Reading post {post.Id} {post.Title}..");
                foreach (var (token, suggestedReplacement) in suggestions)
                {
                    Console.Write($"Bad word {GetRowAndColumnSourceIndex(post.MarkdownContent, token.SourceLocation.SourceIndex)}:");
                    Console.Write($" {token.Token.Trim('*')}");
                    Console.Write($" => {suggestedReplacement.Trim('*')}");
                    Console.WriteLine();
                }

                // Write the corrected content to disk
                var files = new DirectoryInfo(postFolderPath).EnumerateFiles($"{post.Id},*.txt").Take(2).ToArray();
                if (files.Length != 1)
                    throw new Exception("Could not locate source file for Post " + post.Id);
                var correctedMarkdownContent = suggestions
                    .OrderByDescending(suggestion => suggestion.Token.SourceLocation.SourceIndex)
                    .Aggregate(
                        post.MarkdownContent,
                        (markdown, suggestion) =>
                        {
                            var indexOfTokenToReplace = suggestion.Token.SourceLocation.SourceIndex;
                            var indexAfterTokenToReplace = indexOfTokenToReplace + suggestion.Token.SourceLocation.SourceTokenLength;

                            var contentBeforeSuggestion = markdown[..indexOfTokenToReplace];
                            var contentAfterSuggestion = markdown[indexAfterTokenToReplace..];
                            return contentBeforeSuggestion + suggestion.SuggestedReplacement + contentAfterSuggestion;
                        });
                await File.WriteAllTextAsync(files[0].FullName, correctedMarkdownContent);
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
            if (knownWordLookup(source) || source.IsNumber())
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
                if (subTokens.All(t => t.IsNumber() || knownWordLookup(t)))
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

        static (int Column, int Row) GetRowAndColumnSourceIndex(string source, int sourceIndex)
        {
            var rowIndex = 0;
            var colIndex = 0;
            foreach (var c in source.NormaliseLineEndingsWithoutAffectingCharacterIndexes().Take(sourceIndex))
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
    }
}