using GenerateSimilarityEmbeddings;
using MessagePack;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;

#pragma warning disable SKEXP0050 // TextChunker "is for evaluation purposes only and is subject to change or removal in future updates"
#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

const string modelFilePath = "embedding model.onnx";
const string vocabFilePath = "embedding model vocab.txt";

const string embeddingsCacheFilePath = "embeddings.bin";

if (!File.Exists(modelFilePath))
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} Downloading embedding model (this may take minute or two)..");

    using var httpClient = new HttpClient();
    await Task.WhenAll(
        Download("https://huggingface.co/intfloat/e5-base-v2/resolve/main/onnx/model.onnx", modelFilePath),
        Download("https://huggingface.co/intfloat/e5-base-v2/resolve/main/onnx/vocab.txt", vocabFilePath));

    async Task Download(string uri, string filePath)
    {
        using var saveToDiskStream = new FileStream(filePath, FileMode.Create);
        await (await httpClient.GetStreamAsync(uri)).CopyToAsync(saveToDiskStream);
    }
}

var embeddingGenerationService = await BertOnnxTextEmbeddingGenerationService.CreateAsync(modelFilePath, vocabFilePath);

IReadOnlyCollection<IndexablePostChunk> chunks;
if (File.Exists(embeddingsCacheFilePath))
{
    Console.WriteLine("Reading embeddings cache file..");

    using var readEmbeddingsFromDiskStream = new FileStream(embeddingsCacheFilePath, FileMode.Open);
    chunks = await MessagePackSerializer.DeserializeAsync<IReadOnlyCollection<IndexablePostChunk>>(readEmbeddingsFromDiskStream);
}
else
{
    Console.WriteLine("Chunking up blog posts..");

    var textChunks = new List<(int PostId, string Text)>();
    await foreach (var (postId, title, text) in BlogPostReader.Read(new DirectoryInfo("Posts").EnumerateFiles("*.txt")))
    {
        // Note: e5-base-v2 supports vectorisation of content that is up to 512 tokens long (if a different model is used then a different
        // value here may be required)
        const int maxTokensForEmbeddingModel = 512;

        // These are fairly arbitrary values
        const int numberOfOverlapTokens = 100;
        const int minNumberOfCharactersInChunk = 100; // Very short chunks can get high similarity scores, which are usually nonsense

        var lines = TextChunker.SplitPlainTextLines(title + "\n" + text, maxTokensForEmbeddingModel);
        textChunks.AddRange(
            TextChunker.SplitPlainTextParagraphs(lines, maxTokensForEmbeddingModel, overlapTokens: numberOfOverlapTokens)
                .Where(textChunk => textChunk.Length >= minNumberOfCharactersInChunk)
                .Select(textChunk => (postId, textChunk)));
    }

    // Note: e5-base-v2 requires the strings that are to be searched over vectorised for the search index to be prefixed with "passage:"
    Console.WriteLine("Generating embeddings took.. (this may take up to five or ten minutes)");
    var embeddings = await embeddingGenerationService.GenerateEmbeddingsAsync(textChunks.Select(chunk => "passage: " + chunk.Text).ToList());

    chunks = textChunks
        .Zip(embeddings)
        .Select((combined, index) => new IndexablePostChunk(Id: index, combined.First.PostId, combined.First.Text, combined.Second))
        .ToArray();

    Console.WriteLine("Writing embeddings cache file..");
    using var writeEmbeddingsToDiskStream = new FileStream(embeddingsCacheFilePath, FileMode.Create);
    await MessagePackSerializer.SerializeAsync(writeEmbeddingsToDiskStream, chunks);
}

var collection = new InMemoryVectorStoreRecordCollection<int, IndexablePostChunk>("posts");
await collection.CreateCollectionAsync();

// We need to enumerate the UpsertBatchAsync return value to confirm that they were all inserted
await collection.UpsertBatchAsync(chunks).ToArrayAsync();

Console.WriteLine();

while (true)
{
    Console.Write("Type your query and press [Enter]: ");

    var query = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(query))
    {
        Console.WriteLine("You didn't provide any terms - try again!");
        Console.WriteLine();
        continue;
    }

    // Note: e5-base-v2 requires query strings to be prefixed with "query:"
    var queryVector = await embeddingGenerationService.GenerateEmbeddingAsync("query: " + query);

    const int maxNumberOfPosts = 3;
    const int maxNumberOfChunksToConsider = maxNumberOfPosts * 5;

    var resultsEnumerator = await collection.VectorizedSearchAsync(queryVector, new VectorSearchOptions { Top = maxNumberOfChunksToConsider });
    var resultsForPosts = (await resultsEnumerator.Results.ToArrayAsync())
        .GroupBy(result => result.Record.PostId)
        .Select(group => group.OrderByDescending(result => result.Score).First())
        .OrderByDescending(result => result.Score)
        .ToArray();

    if (resultsForPosts.Length == 0)
    {
        Console.WriteLine("No results :(");
        Console.WriteLine();
        continue;
    }

    foreach (var result in resultsForPosts)
    {
        Console.WriteLine($"Post {result.Record.PostId} (Chunk {result.Record.Id}, Score {result.Score:0.000})");
        Console.WriteLine(result.Record.Text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\n', ' '));
        Console.WriteLine();
    }
}