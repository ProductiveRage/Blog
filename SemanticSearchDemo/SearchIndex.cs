using GenerateSimilarityEmbeddings;
using MessagePack;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;

namespace SemanticSearchDemo;

#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

public sealed class SearchIndex
{
    private readonly InMemoryVectorStoreRecordCollection<int, IndexablePostChunk> _vectorStoreCollectionForPosts;
    private readonly BertOnnxTextEmbeddingGenerationService _embeddingGenerationService;

    private SearchIndex(InMemoryVectorStoreRecordCollection<int, IndexablePostChunk> vectorStoreCollectionForPosts, BertOnnxTextEmbeddingGenerationService embeddingGenerationService)
    {
        _vectorStoreCollectionForPosts = vectorStoreCollectionForPosts;
        _embeddingGenerationService = embeddingGenerationService;
    }

    public static async Task<(SearchIndex? SearchIndex, string? Error)> Load(string modelFilePath, string vocabFilePath, string vectorisedChunksCacheFilePath)
    {
        if (!File.Exists(modelFilePath) || !File.Exists(vocabFilePath) || !File.Exists(vectorisedChunksCacheFilePath))
        {
            return (null, "Missing cache file(s)");
        }

        try
        {
            using var readEmbeddingsFromDiskStream = new FileStream(vectorisedChunksCacheFilePath, FileMode.Open);
            var chunks = await MessagePackSerializer.DeserializeAsync<IReadOnlyCollection<IndexablePostChunk>>(readEmbeddingsFromDiskStream);

            var vectorStoreCollectionForPosts = new InMemoryVectorStoreRecordCollection<int, IndexablePostChunk>("posts");
            await vectorStoreCollectionForPosts.CreateCollectionAsync();
            await vectorStoreCollectionForPosts.UpsertBatchAsync(chunks).ToArrayAsync(); // ToArrayAsync evalutes result to confirm they were all inserted

            var embeddingGenerationService = await BertOnnxTextEmbeddingGenerationService.CreateAsync(modelFilePath, vocabFilePath);

            return (new SearchIndex(vectorStoreCollectionForPosts, embeddingGenerationService), null);
        }
        catch (Exception e)
        {
            return (null, "Invalid cache files: " + e.Message);
        }
    }

    public async Task<IReadOnlyCollection<(int PostId, string Excerpt, double Score)>> Search(string query)
    {
        const int maxNumberOfPosts = 3;
        const int maxNumberOfChunksToConsider = maxNumberOfPosts * 5;

        // This KINDA works with e5-base-v2 (it's not ideal, something like a subsequent rereanker step would be better for removing
        // least-bad results that are still poor enough matches that they shouldn't be returned)
        const double similarityThreshold = 0.8d;

        // Note: e5-base-v2 requires query strings to be prefixed with "query:"
        var queryVector = await _embeddingGenerationService.GenerateEmbeddingAsync("query: " + query);
        var resultsEnumerator = await _vectorStoreCollectionForPosts.VectorizedSearchAsync(queryVector, new VectorSearchOptions { Top = maxNumberOfChunksToConsider });
        return (await resultsEnumerator.Results.ToArrayAsync())
            .Where(result => result.Score >= similarityThreshold)
            .GroupBy(result => result.Record.PostId)
            .Select(group => group.OrderByDescending(result => result.Score).First())
            .OrderByDescending(result => result.Score)
            .Select(result =>
            {
                const int excerptTargetLength = 200;

                var excerpt = result.Record.Text.Replace('\n', ' ');
                if (excerpt.Length > excerptTargetLength + 10)
                {
                    excerpt = excerpt[0..excerptTargetLength] + "..";
                }

                // Note: We know that result.Score has a value because we filtered against a threshold above
                return (result.Record.PostId, excerpt, result.Score!.Value);
            })
            .ToArray();
    }
}