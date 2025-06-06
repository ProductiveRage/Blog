using Microsoft.SemanticKernel.Text;

namespace SemanticSearchDemo.Reranking;

#pragma warning disable SKEXP0050 // TextChunker "is for evaluation purposes only and is subject to change or removal in future updates"

public sealed class CohereReranker(string apiKey, HttpClient httpClient) : IReranker
{
    public string Model => "rerank-v3.5";

    public float GetRecommendedThreshold(string query)
    {
        return 0.4f; // TODO: Is RecommendedThreshold good.. or should we look for fall-off in score?);
    }

    public async Task<IReadOnlyCollection<float>> Rerank(string query, IReadOnlyList<RerankerDocument> documents, CancellationToken cancellationToken = default)
    {
        var rerankerRequest = new
        {
            model = Model,
            query,
            documents = documents.Select(document =>
            {
                // TODO: Explain
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

        // TODO: Error handling

        return (await rerankerResponse.Content.ReadFromJsonAsync<RerankerResponse>(cancellationToken))!
            .Results
            .OrderBy(result => result.Index)
            .Select(result => result.Relevance_Score)
            .ToArray();
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