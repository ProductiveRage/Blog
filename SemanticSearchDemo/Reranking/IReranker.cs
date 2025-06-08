using SemanticSearchDemo.Fun;

namespace SemanticSearchDemo.Reranking;

internal interface IReranker
{
    string Model { get; }

    float GetRecommendedThreshold(string query);

    Task<ResultOrError<IReadOnlyCollection<float>>> Rerank(string query, IReadOnlyList<RerankerDocument> documents, CancellationToken cancellationToken = default);
}