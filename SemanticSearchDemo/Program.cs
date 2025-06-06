using System.Net.Mime;
using System.Reflection;
using SemanticSearchDemo;
using SemanticSearchDemo.Fun;
using SemanticSearchDemo.Reranking;
using SemanticSearchDemo.VectorSearch;
using SemanticSearchDemoShared;
using static SemanticSearchDemo.Fun.ResultOrErrorHelpers;

var outputFolderPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;
var modelFilePath = Path.Combine(outputFolderPath, "embedding model.onnx");
var vocabFilePath = Path.Combine(outputFolderPath, "embedding model vocab.txt");
var vectorisedChunksCacheFilePath = Path.Combine(outputFolderPath, "embeddings.bin");
var blogPostsFolderPath = Path.Combine(outputFolderPath, "Posts");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.debug.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine($"Starting SemanticSearchDemo {DateTime.UtcNow:d MMM yyyy HH:mm:ss}");

// Note: e5-base-v2 requires query strings to be prefixed with "query:" and indexed chunks to be prefixed with "passage"
const string queryPrefix = "query:";
const string passagePrefix = "passage:";

// Specify a zero similarity score threshold, because it's not reliable with these embedding models and the reranker
// will do a much job jobof separating the wheat from the chaff
const float defaultSimilarityThreshold = 0;

var rerankedSearch = await (await SearchIndex.Load(modelFilePath, vocabFilePath, vectorisedChunksCacheFilePath, queryPrefix, passagePrefix, defaultSimilarityThreshold))
    .MapError(error => $"Failure.. have you run the {nameof(GenerateSimilarityEmbeddings)} project first, to build the embeddings data?\n\n{error}")
    .Bind(searchIndex =>
    {
        var reranker = configuration["COHERE_API_KEY"] is string cohereApiKey
            ? new CohereReranker(
                cohereApiKey,
                new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) }))
            : ResultOrError<IReranker>.FromError("Reranker configuration missing");

        return reranker.Map(reranker => (SearchIndex: searchIndex, Reranker: reranker));
    })
    .Bind(async searchIndexAndReranker =>
    {
        var originalBlogPosts = await Try(() => BlogPostReader.Read(new DirectoryInfo(blogPostsFolderPath).EnumerateFiles("*.txt")).ToArrayAsync());
        return originalBlogPosts.Map(blogPosts => (searchIndexAndReranker.SearchIndex, searchIndexAndReranker.Reranker, BlogPosts: blogPosts));
    })
    .Map(rerankerDependencies => new RerankedSearch(
        rerankerDependencies.SearchIndex,
        rerankerDependencies.Reranker,
        rerankerDependencies.BlogPosts.ToDictionary(post => post.Id)));

string? startupError = null;
rerankedSearch.IfError(error =>
{
    startupError = error;
    Console.WriteLine(startupError);
});

var app = WebApplication.CreateBuilder(args).Build();

app.UseStaticFiles(); // Allow  favicon!

app.MapGet("/", async (HttpContext context) =>
{
    return await rerankedSearch.Match(
        async rerankedSearch =>
        {
            var query = context.Request.Query["q"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(query))
            {
                return context.Request.HasJsonContentType()
                    ? Results.BadRequest("No 'q' query string value to search for")
                    : Results.Content("<form><input name=q autofocus><input type=submit value=Search></form>", MediaTypeNames.Text.Html);
            }

            var customScoreThreshold = float.TryParse(context.Request.Query["minscore"].FirstOrDefault(), out var parsedMinScore)
                ? parsedMinScore
                : (float?)null;

            var logger = new TimingConsoleLogger();
            var results = await rerankedSearch.Search(query, maxNumberOfResults: 10, customRerankerThreshold: null, logger.Log, context.RequestAborted);

            if (context.Request.HasJsonContentType())
            {
                // Note: The JsonSerializer is bad with tuples, so transform the results array items to anonymous types
                return Results.Json(new
                {
                    query,
                    results = results.Select(result => new { result.Post.Id, result.Score, result.Excerpt })
                });
            }

            var resultsContent = results.Count == 0
                ? "No results."
                : string.Join("\n\n", results.Select(result => $"Post {result.Post.Id} (semantic similarity score {result.Score:N5})\n{result.Excerpt}"));

            return Results.Content($"Searching for: {query}\n\n{resultsContent}");
        },
        error => Results.Content(error, statusCode: 500));
});

app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == 404 && !context.Response.HasStarted)
    {
        await context.Response.WriteAsync("Page not found.");
    }
});

app.Run();