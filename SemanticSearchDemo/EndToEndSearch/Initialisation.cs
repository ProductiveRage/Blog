using System.Reflection;
using GenerateSimilarityEmbeddings;
using MessagePack;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using SemanticSearchDemo.Fun;
using SemanticSearchDemo.Reranking;
using SemanticSearchDemo.VectorSearch;
using static SemanticSearchDemo.Fun.ResultOrErrorHelpers;

namespace SemanticSearchDemo.EndToEndSearch;

#pragma warning disable SKEXP0070 // BertOnnxTextEmbeddingGenerationService "is for evaluation purposes only and is subject to change or removal in future updates"

internal static class Initialisation
{
    /// <summary>
    /// Construct a RerankedSearch instance from cached data present in the application output folder
    /// </summary>
    public static async Task<ResultOrError<RerankedSearch>> LoadRerankedSearch(Action<string> log)
    {
        // This will be of the form "/app/bin/Debug/net8.0" in a Debug configuation, since VS does some helpful things to make it
        // easier to iterate quickly with containers when in Debug.. but it can lead to surprises when Release configuration
        // builds will have this path as "/app" and there may be limited permissions available to read files from there, if
        // you stick with the default `USER $APP_UID` line added to Dockerfile since .NET 8.0
        var outputFolderPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;

        var embeddingsFolderPath = Path.Combine(outputFolderPath, "Embeddings");
        var blogPostsFolderPath = Path.Combine(outputFolderPath, "Posts");

        var modelFilePath = Path.Combine(embeddingsFolderPath, "embedding model.onnx");
        var vocabFilePath = Path.Combine(embeddingsFolderPath, "embedding model vocab.txt");
        var vectorisedChunksCacheFilePath = Path.Combine(embeddingsFolderPath, "embeddings.bin");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.debug.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        log($"Starting SemanticSearchDemo {DateTime.UtcNow:d MMM yyyy HH:mm:ss}");

        // Note: e5-base-v2 requires query strings to be prefixed with "query:" and indexed chunks to be prefixed with "passage"
        const string queryPrefix = "query:";
        const string passagePrefix = "passage:";

        // Specify a zero similarity score threshold, because it's not reliable with these embedding models and the reranker
        // will do a much job jobof separating the wheat from the chaff
        const float defaultSimilarityThreshold = 0;

        return await LoadSearchIndex(modelFilePath, vocabFilePath, vectorisedChunksCacheFilePath, queryPrefix, passagePrefix, defaultSimilarityThreshold, log)
            .MapError(error => new Error($"Failure.. have you run the {nameof(GenerateSimilarityEmbeddings)} project first, to build the embeddings data? {error.Message}"))
            .Bind(searchIndex =>
            {
                var reranker = configuration["COHERE_API_KEY"]
                    .ToResultOrError(ifNull: () => "Reranker configuration missing")
                    .Map(cohereApiKey =>
                    {
                        log("Successfully retrieved Cohere API key");
                        return new CohereReranker(
                            cohereApiKey,
                            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) }));
                    });

                return reranker.Map(reranker => (SearchIndex: searchIndex, Reranker: reranker));
            })
            .Bind(async searchIndexAndReranker =>
            {
                var originalBlogPosts = await Try(async () =>
                {
                    var sourceFiles = new DirectoryInfo(blogPostsFolderPath).EnumerateFiles("*.txt");
                    var blogPosts = (await BlogPostReader.Read(sourceFiles).ToArrayAsync()).ToDictionary(post => post.Id);
                    log("Loaded blog post history");
                    return blogPosts;
                });
                return originalBlogPosts.Map(blogPosts => (searchIndexAndReranker.SearchIndex, searchIndexAndReranker.Reranker, BlogPosts: blogPosts));
            })
            .Map(rerankerDependencies =>
            {
                var rerankedSearch = new RerankedSearch(rerankerDependencies.SearchIndex, rerankerDependencies.Reranker, rerankerDependencies.BlogPosts);
                log("Successfully initialised reranked search");
                return rerankedSearch;
            })
            .IfError(error =>
            {
                // Ensure that any error gets logged
                log(error.Message);
            });
    }

    private static async Task<ResultOrError<SearchIndex>> LoadSearchIndex(
        string modelFilePath,
        string vocabFilePath,
        string vectorisedChunksCacheFilePath,
        string queryPrefix,
        string passagePrefix,
        float defaultSimilarityThreshold,
        Action<string> log) =>
            await Try(
                async () =>
                {
                    if (DoAnyFilesNotExist([modelFilePath, vocabFilePath, vectorisedChunksCacheFilePath], log))
                    {
                        // We'll log the paths that we couldn't resolve, but just return a single exception to say that at least one could not be found
                        throw new Exception("Missing cache file(s)");
                    }

                    using var readEmbeddingsFromDiskStream = new FileStream(vectorisedChunksCacheFilePath, FileMode.Open);
                    var chunks = await MessagePackSerializer.DeserializeAsync<IReadOnlyCollection<IndexablePostChunk>>(readEmbeddingsFromDiskStream);
                    log("Loaded vectorised chunks content");

                    var vectorStoreCollection = new InMemoryVectorStoreRecordCollection<int, IndexablePostChunk>("posts");
                    await vectorStoreCollection.CreateCollectionAsync();
                    await vectorStoreCollection.UpsertBatchAsync(chunks).ToArrayAsync(); // ToArrayAsync evalutes result to confirm they were all inserted
                    log("Indexed vectorised chunks");

                    var embeddingGenerationService = await BertOnnxTextEmbeddingGenerationService.CreateAsync(modelFilePath, vocabFilePath);
                    log("Loaded embedding generation service");

                    return (VectorStoreCollectionForPosts: vectorStoreCollection, EmbeddingGenerationService: embeddingGenerationService);
                })
                .Map(vectorStoreAndEmbeddingGenerator => new SearchIndex(
                    vectorStoreAndEmbeddingGenerator.VectorStoreCollectionForPosts,
                    vectorStoreAndEmbeddingGenerator.EmbeddingGenerationService,
                    queryPrefix,
                    passagePrefix,
                    defaultSimilarityThreshold));

    private static bool DoAnyFilesNotExist(IEnumerable<string> filePaths, Action<string> log)
    {
        var encounteredMissingFile = false;
        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                log($"Could not find file {filePath}");
                encounteredMissingFile = true;
            }
            else
            {
                // TODO: Get rid of this
                log($"Successfully located file {filePath}");
            }
        }
        return encounteredMissingFile;
    }
}