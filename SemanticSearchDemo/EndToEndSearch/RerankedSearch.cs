using SemanticSearchDemo.Fun;
using SemanticSearchDemo.Reranking;
using SemanticSearchDemo.VectorSearch;
using SemanticSearchDemoShared;

namespace SemanticSearchDemo.EndToEndSearch;

/// <summary>
/// For performing searches that combine a (textual similarity) vector search, followed by a reranker step
/// </summary>
internal sealed class RerankedSearch(SearchIndex searchIndex, IReranker reranker, IReadOnlyDictionary<int, BlogPost> originalContent)
{
    public sealed record Result(int ChunkId, BlogPost Post, string Excerpt, float Score);

    public async Task<ResultOrError<IReadOnlyCollection<Result>>> Search(
        string searchFor,
        int maxNumberOfResults,
        float? customRerankerThreshold,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        // The maxNumberPerDocument determines how many chunks (at most) will be returned for each document - there's no guarantee that the "best"
        // one in terms of semantic vector similarity will be what the reranker thinks is best, so we'll get up to five for each blost post and
        // run them all through the reranker (we'll never return more than one result for a blog post id, though)
        // - Note: The `customRerankerThreshold` applies to the reranker threshold, NOT the score threshold used by the search index (so a null
        //   value is passed for `customScoreThreshold` in the `searchIndex.Search(..)` call
        var searchIndexResults = await searchIndex.Search(searchFor, maxNumberOfResults, maxNumberPerDocument: 5, customScoreThreshold: null, log, cancellationToken);

        return await searchIndexResults.Bind(searchIndexResults => Rerank(searchFor, searchIndexResults, customRerankerThreshold, log, cancellationToken));
    }

    private async Task<ResultOrError<IReadOnlyCollection<Result>>> Rerank(
        string searchFor,
        IReadOnlyCollection<SearchIndex.Result> searchIndexResults,
        float? customRerankerThreshold,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var annotatedSearchResults = searchIndexResults
            .Select(result => originalContent.TryGetValue(result.PostId, out var originalPost) ? (result.ChunkId, Excerpt: result.Text, Post: originalPost) : default)
            .Where(document => document != default)
            .ToArray();

        if (annotatedSearchResults.Length == 0)
        {
            return Array.Empty<Result>();
        }

        var rerankerScores = await reranker.Rerank(
            searchFor,
            [.. annotatedSearchResults.Select(searchResult => new RerankerDocument(searchResult.Post.Title, searchResult.Post.Text, searchResult.Excerpt))],
            cancellationToken);

        log("Completed reranking");

        return rerankerScores.Bind(rerankerScores => ApplyRerankerScoresToSearchResults(searchFor, annotatedSearchResults, rerankerScores, customRerankerThreshold));
    }

    private ResultOrError<IReadOnlyCollection<Result>> ApplyRerankerScoresToSearchResults(
        string searchFor,
        (int ChunkId, string Excerpt, BlogPost Post)[] annotatedSearchResults,
        IReadOnlyCollection<float> rerankerScores,
        float? customRerankerThreshold)
    {
        if (rerankerScores.Count != annotatedSearchResults.Length)
        {
            return new Error($"The reranker return {rerankerScores.Count} result(s), instead of the expected {annotatedSearchResults.Length}");
        }

        var rerankerThreshold = customRerankerThreshold.GetValueOrDefault(reranker.GetRecommendedThreshold(searchFor));
        return annotatedSearchResults
            .Zip(
                rerankerScores,
                (searchResult, rerankerScore) => new Result(searchResult.ChunkId, Post: searchResult.Post, searchResult.Excerpt, Score: rerankerScore))
            .Where(result => result.Score >= rerankerThreshold)
            .GroupBy(result => result.Post.Id)
            .Select(group => group
                .OrderByDescending(result => result.Score)
                .First())
            .OrderByDescending(result => result.Score)
            .ToArray();
    }
}