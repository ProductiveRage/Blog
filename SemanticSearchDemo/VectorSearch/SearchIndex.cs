using GenerateSimilarityEmbeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;
using SemanticSearchDemo.Fun;
using static SemanticSearchDemo.Fun.ResultOrErrorHelpers;

namespace SemanticSearchDemo.VectorSearch;

#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

internal sealed class SearchIndex(
    InMemoryVectorStoreRecordCollection<int, IndexablePostChunk> vectorStoreCollectionForPosts,
    BertOnnxTextEmbeddingGenerationService embeddingGenerationService,
    float similarityThreshold)
{
    public sealed record Result(int ChunkId, int PostId, string Text, double Score);
    
    private readonly float _similarityThreshold = similarityThreshold;

    public async Task<ResultOrError<IReadOnlyCollection<Result>>> Search(
        string query,
        int maxNumberOfChunksToConsider,
        int maxNumberPerDocument,
        float? customScoreThreshold,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var queryVector = await Try(async () =>
        {
            var queryVector = await embeddingGenerationService.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
            log("Generated query embedding");
            return queryVector;
        });

        var vectorSearchResults = await queryVector
            .Bind(async queryVector =>
            {
                var vectorSearchResultCollection = await vectorStoreCollectionForPosts.VectorizedSearchAsync(
                    queryVector,
                    new VectorSearchOptions { Top = maxNumberOfChunksToConsider },
                    cancellationToken);
                var vectorSearchResults = await vectorSearchResultCollection.Results.ToArrayAsync(cancellationToken);
                log("Executed semantic search");
                return vectorSearchResults;
            });

        return vectorSearchResults
            .Bind<IReadOnlyCollection<Result>>(vectorSearchResults =>
            {
                var similarityThreshold = customScoreThreshold.GetValueOrDefault(_similarityThreshold);
                return vectorSearchResults
                    .Where(result => result.Score >= similarityThreshold)
                    .GroupBy(result => result.Record.PostId)
                    .SelectMany(group => group
                        .OrderByDescending(document => document.Score)
                        .Take(maxNumberPerDocument))
                    .OrderByDescending(result => result.Score)
                    .Select(result => new Result(
                        result.Record.Id,
                        result.Record.PostId,
                        TidyUpExcerpt(result.Record.Text),
                        result.Score!.Value)) // Note: We know that result.Score has a value because we filtered against a threshold above
                    .ToArray();
            });
    }

    private string TidyUpExcerpt(string text) => text.Replace('\n', ' ');
}