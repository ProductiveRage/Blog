namespace SemanticSearchDemo.Reranking;

public sealed class ZeroReranker : IReranker
{
    public static ZeroReranker Instance { get; } = new();
    
    private ZeroReranker() { }

    public string Model => "zero-reranker";
    
    public float GetRecommendedThreshold(string query) => 0;

    public Task<IReadOnlyCollection<float>> Rerank(string query, IReadOnlyList<RerankerDocument> documents, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<float>>([.. documents.Select(_ => 0f)]);
}