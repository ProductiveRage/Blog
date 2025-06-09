using System.Net.Mime;
using SemanticSearchDemo.Fun;

namespace SemanticSearchDemo.EndToEndSearch;

internal static class RerankedSearchExtensions
{
    /// <summary>
    /// Generate a response for a search request (performing a search where a query is specified and search is available, or
    /// returning an error message where not the case)
    /// </summary>
    public static async Task<IResult> PerformSearch(this ResultOrError<RerankedSearch> source, HttpRequest request, CancellationToken cancellationToken) =>
        await source.Bind(async rerankedSearch =>
        {
            var provideJsonResponse =
                request.HasJsonContentType() ||
                request.TryToGetFromQueryString("json") == "1";

            if (request.TryToGetFromQueryString("q") is string query)
            {
                var customRerankerThreshold = request.TryToGetFloatFromQueryString("minscore");
                var logger = new TimingConsoleLogger();
                return await rerankedSearch.Search(query, maxNumberOfResults: 10, customRerankerThreshold, logger.Log, cancellationToken)
                    .Map(results => provideJsonResponse
                        ? results.ToJson(query)
                        : results.ToHtml(query));
            }

            return ResultOrError.FromResult(provideJsonResponse
                ? Results.BadRequest("No 'q' query string value to search for")
                : Results.Content("<form><input name=q autofocus><input type=submit value=Search></form>", MediaTypeNames.Text.Html));
        })
        .MapErrorToResult(error => Results.Content(error.Message, statusCode: 500));

    private static string? TryToGetFromQueryString(this HttpRequest request, string name) => request.Query[name].FirstOrDefault();

    private static float? TryToGetFloatFromQueryString(this HttpRequest request, string name) =>
        float.TryParse(TryToGetFromQueryString(request, name), out var result)
            ? result
            : null;

    private static IResult ToHtml(this IReadOnlyCollection<RerankedSearch.Result> source, string query) =>
        Results.Content(
            $"Searching for: {query}\n\n" +
            (source.Count == 0
                ? "No results."
                : string.Join("\n\n", source.Select(result => $"Chunk {result.ChunkId} of Post {result.Post.Id} (reranker score {result.Score:N5})\n{result.Excerpt}"))));

    // Note: The JsonSerializer is bad with tuples, so transform the results array items to anonymous types
    private static IResult ToJson(this IReadOnlyCollection<RerankedSearch.Result> source, string query) =>
        Results.Json(new
        {
            query,
            results = source.Select(result => new { result.Post.Id, result.Score, result.Excerpt })
        });
}
