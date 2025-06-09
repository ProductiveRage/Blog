using System.Runtime.CompilerServices;
using GenerateSimilarityEmbeddings;
using MessagePack;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;

#pragma warning disable SKEXP0050 // TextChunker "is for evaluation purposes only and is subject to change or removal in future updates"
#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

// Write the embeddings files into a folder that is a sibling of this and the SemanticSearchDemo project, so that it's
// easier to pull them into the SemanticSearchDemo (but in such a way that it's not a compile error if you try to
// build that project before this project has run successfully)
var embeddingsFolderPath = Path.Combine(GetProjectFolderPath(), "..\\Embeddings");
var embeddingModelFile = new FileInfo(Path.Combine(embeddingsFolderPath, "embedding model.onnx"));
var embeddingModelVocabFile = new FileInfo(Path.Combine(embeddingsFolderPath, "embedding model vocab.txt"));
var vectorisedChunksCacheFile = new FileInfo(Path.Combine(embeddingsFolderPath, "embeddings.bin"));

if (!embeddingModelFile.Exists)
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} Downloading embedding model (this may take minute or two)..");

    // Note: If a different model is used, the vector dimensions in IndexablePostChunk may need to be changed
    using var httpClient = new HttpClient();
    await Task.WhenAll(
        Download("https://huggingface.co/intfloat/e5-base-v2/resolve/main/onnx/model.onnx", embeddingModelFile),
        Download("https://huggingface.co/intfloat/e5-base-v2/resolve/main/onnx/vocab.txt", embeddingModelVocabFile));

    async Task Download(string uri, FileInfo destination)
    {
        EnsureFolderExistsForFile(destination);
        using var saveToDiskStream = new FileStream(destination.FullName, FileMode.Create);
        await (await httpClient.GetStreamAsync(uri)).CopyToAsync(saveToDiskStream);
    }
}

var embeddingGenerationService = await BertOnnxTextEmbeddingGenerationService.CreateAsync(
    embeddingModelFile.FullName,
    embeddingModelVocabFile.FullName);

IReadOnlyCollection<IndexablePostChunk> chunks;
if (vectorisedChunksCacheFile.Exists)
{
    Console.WriteLine("Reading embeddings cache file..");

    using var readEmbeddingsFromDiskStream = new FileStream(vectorisedChunksCacheFile.FullName, FileMode.Open);
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
        .ToArray()
        .AsReadOnly();

    Console.WriteLine("Writing embeddings cache file..");
    EnsureFolderExistsForFile(vectorisedChunksCacheFile);
    using var writeEmbeddingsToDiskStream = new FileStream(vectorisedChunksCacheFile.FullName, FileMode.Create);
    await MessagePackSerializer.SerializeAsync(writeEmbeddingsToDiskStream, chunks);
}

var vectorStoreCollectionForPosts = new InMemoryVectorStoreRecordCollection<int, IndexablePostChunk>("posts");
await vectorStoreCollectionForPosts.CreateCollectionAsync();

// We need to enumerate the UpsertBatchAsync return value to confirm that they were all inserted
await vectorStoreCollectionForPosts.UpsertBatchAsync(chunks).ToArrayAsync();

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

    // This KINDA works with e5-base-v2 (it's not ideal, something like a subsequent rereanker step would be better for removing
    // least-bad results that are still poor enough matches that they shouldn't be returned)
    const double similarityThreshold = 0.8d;

    var resultsEnumerator = await vectorStoreCollectionForPosts.VectorizedSearchAsync(queryVector, new VectorSearchOptions { Top = maxNumberOfChunksToConsider });
    var resultsForPosts = (await resultsEnumerator.Results.ToArrayAsync())
        .Where(result => result.Score >= similarityThreshold)
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

static string GetProjectFolderPath()
{
    var folder = new FileInfo(GetFullPathOfCurentFile()).Directory ?? throw new Exception("Unable to extract folder from [CallerFilePath] value");
    return folder.FullName;

    static string GetFullPathOfCurentFile([CallerFilePath] string? path = null) =>
        path ?? throw new Exception("Unable to resolve current file path using [CallerFilePath]");
}

static void EnsureFolderExistsForFile(FileInfo destination)
{
    if ((destination.Directory is not null) && !destination.Directory.Exists)
    {
        destination.Directory.Create();
    }
}