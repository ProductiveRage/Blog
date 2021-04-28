using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlogBackEnd.Models;
using Catalyst;
using Catalyst.Models;
using HNSW.Net;
using Markdig;
using Mosaik.Core;
using UID;

namespace AutomatedSimilarPostGenerator
{
    internal static class Recommender
    {
        public static async Task<IEnumerable<(Post Post, IEnumerable<(Post Post, float SimilarityDistance, float ProximityByTitleTFIDF)> Similar)>> GetSimilarPosts(
            IEnumerable<Post> posts,
            int epoch = 50,
            int dimensions = 512,
            int minimumCount = 1,
            int contextWindow = 10,
            int negativeSamplingCount = 20,
            int maximumNumberOfResultsToReturn = 3)
        {
            Console.WriteLine("Parsing documents..");
            Storage.Current = new OnlineRepositoryStorage(new DiskStorage("catalyst-models"));
            var language = Language.English;
            var pipeline = Pipeline.For(language);
            var postsWithDocuments = posts
                .Select(post =>
                {
                    var plainText = Markdown.ToPlainText(post.MarkdownContent);
                    var document = new Document(NormaliseSomeCommonTerms(plainText), language)
                    {
                        UID = post.Title.Hash128()
                    };
                    pipeline.ProcessSingle(document);
                    return (Post: post, Document: document);
                })
                .ToArray(); // Call ToArray to force evaluation of the document processing now

            Console.WriteLine("Training FastText model..");
            var fastText = new FastText(language, version: 0, tag: "");
            fastText.Data.Type = FastText.ModelType.PVDM;
            fastText.Data.Loss = FastText.LossType.NegativeSampling;
            fastText.Data.IgnoreCase = true;
            fastText.Data.Epoch = epoch;
            fastText.Data.Dimensions = dimensions;
            fastText.Data.MinimumCount = minimumCount;
            fastText.Data.ContextWindow = contextWindow;
            fastText.Data.NegativeSamplingCount = negativeSamplingCount;
            fastText.Train(
                postsWithDocuments.Select(postsWithDocument => postsWithDocument.Document),
                trainingStatus: update => Console.WriteLine($" Progress: {update.Progress}, Epoch: {update.Epoch}")
            );

            Console.WriteLine("Training TF-IDF model..");
            var tfidf = new TFIDF(pipeline.Language, version: 0, tag: "");
            await tfidf.Train(postsWithDocuments.Select(postWithDocument => postWithDocument.Document));

            Console.WriteLine("Getting average TF-IDF weights per word..");
            var tokenValueTFIDF = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in postsWithDocuments.Select(postWithDocument => postWithDocument.Document))
            {
                tfidf.Process(doc);
                foreach (var sentence in doc)
                {
                    foreach (var token in sentence)
                    {
                        if (!tokenValueTFIDF.TryGetValue(token.Value, out var freqs))
                        {
                            freqs = new();
                            tokenValueTFIDF.Add(token.Value, freqs);
                        }
                        freqs.Add(token.Frequency);
                    }
                }
            }
            var averagedTokenValueTFIDF = tokenValueTFIDF.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Average(), StringComparer.OrdinalIgnoreCase
            );

            Console.WriteLine("Building recommendations..");

            // Combine the blog post data with the FastText-generated vectors
            var results = fastText
                .GetDocumentVectors()
                .Select(result =>
                {
                    // Each document vector instance will include a "token" string that may be mapped back to the UID of the document for each blog post. If there were a large number of posts
                    // to deal with then a dictionary to match UIDs to blog posts would be sensible for performance but I only have a 100+ and so a LINQ "First" scan over the list will suffice.
                    var uid = UID128.Parse(result.Token);
                    var postForResult = postsWithDocuments.First(
                        postWithDocument => postWithDocument.Document.UID == uid
                    );
                    return (UID: uid, result.Vector, postForResult.Post);
                })
                .ToArray(); // ToArray since we enumerate multiple times below

            // Construct a graph to search over, as described at https://github.com/curiosity-ai/hnsw-sharp#how-to-build-a-graph
            var graph = new SmallWorld<(UID128 UID, float[] Vector, Post Post), float>(
                distance: (to, from) => CosineDistance.NonOptimized(from.Vector, to.Vector),
                DefaultRandomGenerator.Instance,
                new() { M = 15, LevelLambda = 1 / Math.Log(15) }
            );
            graph.AddItems(results);

            // For every post, use the "KNNSearch" method on the graph to find the three most similar posts
            return results
                .Select(result =>
                {
                    // Request that the KNNSearch operate over all documents because we can't take the top {n} until we've combined the ordering with the title TFIDF proximity values
                    var similarResults = graph
                        .KNNSearch(result, postsWithDocuments.Length)
                        .Where(similarResult => similarResult.Item.UID != result.UID);

                    var tokenValuesInTitle = GetAllTokensForText(NormaliseSomeCommonTerms(result.Post.Title), pipeline)
                        .Select(token => token.Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    return (
                        result.Post,
                        similarResults
                            .Select(similarResult => (
                                similarResult.Item.Post,
                                similarResult.Distance,
                                ProximityByTitleTFIDF: GetProximityByTitleTFIDF(similarResult.Item.Post.Title, tokenValuesInTitle, averagedTokenValueTFIDF, pipeline)
                            ))
                            .OrderByDescending(similarResult => similarResult.ProximityByTitleTFIDF)
                            .ThenBy(similarResult => similarResult.Distance)
                            .Take(maximumNumberOfResultsToReturn)
                            .ToArray() // Call ToArray to do this work now, rather than when it's evaluated
                            .AsEnumerable()
                    );
                })
                .ToArray(); // Call ToArray to do this work now, rather than when it's evaluated
        }

        private static float GetProximityByTitleTFIDF(string similarPostTitle, HashSet<string> tokenValuesInInitialPostTitle, Dictionary<string, float> averagedTokenValueTFIDF, Pipeline pipeline)
        {
            return GetAllTokensForText(NormaliseSomeCommonTerms(similarPostTitle), pipeline)
                .Where(token => tokenValuesInInitialPostTitle.Contains(token.Value))
                .Sum(token =>
                {
                    var tfidfValue = averagedTokenValueTFIDF.TryGetValue(token.Value, out var score) ? score : 0;
                    if (tfidfValue <= 0)
                    {
                        // Ignore any tokens that report a negative impact (eg. punctuation or really common words like "in")
                        return 0;
                    }
                    return tfidfValue;
                });
        }

        private static IEnumerable<IToken> GetAllTokensForText(string text, Pipeline pipeline)
        {
            var doc = new Document(text, pipeline.Language);
            pipeline.ProcessSingle(doc);
            return doc.SelectMany(sentence => sentence);
        }

        private static string NormaliseSomeCommonTerms(string text) => text
            .Replace(".NET", "NET", StringComparison.OrdinalIgnoreCase)
            .Replace("Full Text Indexer", "FullTextIndexer", StringComparison.OrdinalIgnoreCase)
            .Replace("Bridge.net", "BridgeNET", StringComparison.OrdinalIgnoreCase)
            .Replace("React", "ReactJS");
    }
}