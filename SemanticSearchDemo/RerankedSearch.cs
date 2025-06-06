using SemanticSearchDemo.Reranking;
using SemanticSearchDemo.VectorSearch;
using SemanticSearchDemoShared;

namespace SemanticSearchDemo;

public sealed class RerankedSearch(SearchIndex searchIndex, IReranker reranker, IReadOnlyDictionary<int, BlogPost> originalContent)
{
    public async Task<IReadOnlyCollection<(int ChunkId, (int Id, string Title, string Text) Post, string Excerpt, float Score)>> Search(
        string searchFor,
        int maxNumberOfResults,
        float? customRerankerThreshold,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        // The maxNumberPerDocument determines how many chunks (at most) will be returned for each document - there's no guarantee that the "best"
        // one in terms of semantic vector similarity will be what the reranker thinks is best, so we'll get up to five for each blost post and
        // run them all through the reranker (we'll never return more than one result for a blog post id from this method, though)
        var searchIndexResults = await searchIndex.Search(searchFor, maxNumberOfResults, maxNumberPerDocument: 5, customScoreThreshold: null, log, cancellationToken);

        var annotatedSearchResults = searchIndexResults
            .Select(result => originalContent.TryGetValue(result.PostId, out var originalPost)
                ? (result.ChunkId, result.PostId, RerankerDocument: new RerankerDocument(originalPost.Title, FullText: originalPost.Text, Excerpt: result.Text))
                : default)
            .Where(document => document != default)
            .ToArray();

        var rerankerScores = annotatedSearchResults.Length > 0
            ? await reranker.Rerank(searchFor, [.. annotatedSearchResults.Select(entry => entry.RerankerDocument)], cancellationToken)
            : [];

        log("Completed reranking");

        if (rerankerScores.Count != annotatedSearchResults.Length)
        {
            throw new Exception($"The reanker return {rerankerScores.Count} result(s), instead of the expected {annotatedSearchResults.Length}");
        }

        var rerankerThreshold = customRerankerThreshold.GetValueOrDefault(reranker.GetRecommendedThreshold(searchFor));

#pragma warning disable IDE0305 // Simplify collection initialization (2025-03-21 DWR: I prefer the explicit ToArray call)
        return annotatedSearchResults
            .Zip(
                rerankerScores,
                (result, rerankerScore) => (
                    result.ChunkId,
                    Post: (Id: result.PostId, result.RerankerDocument.Title, result.RerankerDocument.FullText),
                    result.RerankerDocument.Excerpt,
                    RerankerScore: rerankerScore))
            .Where(result => result.RerankerScore >= rerankerThreshold)
            .GroupBy(result => result.Post.Id)
            .Select(group => group
                .OrderByDescending(result => result.RerankerScore)
                .First())
            .OrderByDescending(result => result.RerankerScore)
            .ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization
    }
}