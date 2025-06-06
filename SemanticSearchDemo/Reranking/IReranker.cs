namespace SemanticSearchDemo.Reranking;

public interface IReranker
{
    string Model { get; }

    float GetRecommendedThreshold(string query);

    Task<IReadOnlyCollection<float>> Rerank(string query, IReadOnlyList<RerankerDocument> documents, CancellationToken cancellationToken = default);
}