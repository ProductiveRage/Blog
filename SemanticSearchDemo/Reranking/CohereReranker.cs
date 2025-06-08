using Microsoft.SemanticKernel.Text;
using SemanticSearchDemo.Fun;

namespace SemanticSearchDemo.Reranking;

#pragma warning disable SKEXP0050 // TextChunker "is for evaluation purposes only and is subject to change or removal in future updates"

internal sealed class CohereReranker(string apiKey, HttpClient httpClient) : IReranker
{
    public string Model => "rerank-v3.5";

    public float GetRecommendedThreshold(string query)
    {
        // When there are only a couple of words (and three is a very arbitrary value to have chosen), it's much more likely a keyword search than
        // natural language, and the reranker is much more effective with natural language - so allow a lower score threshold in these cases
        if (query.Split().Length <= 3)
        {
            return 0.2f;
        }

        // This will suffice for now, but it the recommended approach (https://docs.cohere.com/v2/docs/reranking-best-practices#interpreting-results)
        // is to use border relevant queries against data in the set and look at the scores returned, and to use an average from those (I've done a
        // smalll selection searches on my blog data - as of May 2026 - and picked 0.4 for now)
        return 0.4f;
    }

    public async Task<ResultOrError<IReadOnlyCollection<float>>> Rerank(string query, IReadOnlyList<RerankerDocument> documents, CancellationToken cancellationToken = default)
    {
        var rerankerRequest = new
        {
            model = Model,
            query,
            documents = documents.Select(document =>
            {
                // If we can fit the Title and the FullText into a single chunk (if it's a short post) then we'll do that - if not, we'll combine
                // the Title, the "Excerpt" (ie. the text from the current chunk of the post), the FullText, and then truncate it (this should
                // allow us to pack as much relevant content into the available space)
                var optimisticCombination = Truncate($"{document.Title}\n\n{document.FullText}");
                return optimisticCombination.Contains(document.Excerpt)
                    ? optimisticCombination
                    : Truncate($"{document.Title}\n\n{document.Excerpt}\n\n{document.FullText}");
            }),
            top_n = documents.Count
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/rerank");
        requestMessage.Headers.Authorization = new("Bearer", apiKey);
        requestMessage.Content = JsonContent.Create(rerankerRequest);

        var rerankerResponse = await httpClient.SendAsync(requestMessage, cancellationToken);
        if (!rerankerResponse.IsSuccessStatusCode)
        {
            return new Error($"{GetType().Name} received {rerankerResponse.StatusCode} failure status from http call");
        }

        try
        {
            return (await rerankerResponse.Content.ReadFromJsonAsync<RerankerResponse>(cancellationToken))!
                .Results
                .OrderBy(result => result.Index)
                .Select(result => result.Relevance_Score)
                .ToArray();
        }
        catch (Exception e)
        {
            return new Error($"{GetType().Name} received to parse response: {e.Message}");
        }
    }

    private static string Truncate(string content)
    {
        const int maxTokensInDocuments = 4_096;

        var lines = TextChunker.SplitPlainTextLines(content, maxTokensInDocuments);
        return TextChunker.SplitPlainTextParagraphs(lines, maxTokensInDocuments).First();
    }

    private sealed record RerankerResponse(RerankerResult[] Results);

    private sealed record RerankerResult(int Index, float Relevance_Score);
}