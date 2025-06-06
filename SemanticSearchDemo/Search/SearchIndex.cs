using GenerateSimilarityEmbeddings;
using MessagePack;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;

namespace SemanticSearchDemo.Search;

#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

public sealed class SearchIndex
{
    private readonly InMemoryVectorStoreRecordCollection<int, IndexablePostChunk> _vectorStoreCollectionForPosts;
    private readonly BertOnnxTextEmbeddingGenerationService _embeddingGenerationService;
    private readonly string _queryPrefix, _passagePrefix;
    private readonly float _similarityThreshold;

    private SearchIndex(
        InMemoryVectorStoreRecordCollection<int, IndexablePostChunk> vectorStoreCollectionForPosts,
        BertOnnxTextEmbeddingGenerationService embeddingGenerationService,
        string queryPrefix,
        string passagePrefix,
        float similarityThreshold)
    {
        _vectorStoreCollectionForPosts = vectorStoreCollectionForPosts;
        _embeddingGenerationService = embeddingGenerationService;
        _queryPrefix = queryPrefix;
        _passagePrefix = passagePrefix;
        _similarityThreshold = similarityThreshold;
    }

    public static async Task<(SearchIndex? SearchIndex, string? Error)> Load(
        string modelFilePath,
        string vocabFilePath,
        string vectorisedChunksCacheFilePath,
        string queryPrefix,
        string passagePrefix,
        float defaultSimilarityThreshold)
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
            return (
                SearchIndex: new SearchIndex(vectorStoreCollectionForPosts, embeddingGenerationService, queryPrefix, passagePrefix, defaultSimilarityThreshold),
                Error: null);
        }
        catch (Exception e)
        {
            return (null, "Invalid cache files: " + e.Message);
        }
    }

    public async Task<IReadOnlyCollection<(int ChunkId, int PostId, string Text, double Score)>> Search(
        string query,
        int maxNumberOfChunksToConsider,
        int maxNumberPerDocument,
        float? customScoreThreshold,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var queryVector = await _embeddingGenerationService.GenerateEmbeddingAsync(_queryPrefix + query, cancellationToken: cancellationToken);
        log("Generated query embedding");

        var vectorSearchResults = await (await _vectorStoreCollectionForPosts.VectorizedSearchAsync(
            queryVector,
            new VectorSearchOptions { Top = maxNumberOfChunksToConsider },
            cancellationToken))
                .Results
                .ToArrayAsync(cancellationToken);
        log("Executed semantic search");

        var similarityThreshold = customScoreThreshold.GetValueOrDefault(_similarityThreshold);
        return vectorSearchResults
            .Where(result => result.Score >= similarityThreshold)
            .GroupBy(result => result.Record.PostId)
            .SelectMany(group => group
                .OrderByDescending(document => document.Score)
                .Take(maxNumberPerDocument))
            .OrderByDescending(result => result.Score)
            .Select(result => (result.Record.Id, result.Record.PostId, TidyUpExcerpt(result.Record.Text), result.Score!.Value)) // Note: We know that result.Score has a value because we filtered against a threshold above
            .ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization
    }

    private string TidyUpExcerpt(string text) => RemovePassagePrefix(text).Replace('\n', ' ');

    private string RemovePassagePrefix(string text) =>
        text.StartsWith(_passagePrefix)
            ? text[_passagePrefix.Length..].TrimStart()
            : text;
}