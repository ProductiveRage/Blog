using System.Net.Mime;
using System.Reflection;
using SemanticSearchDemo;
using SemanticSearchDemo.Reranking;
using SemanticSearchDemo.Search;
using SemanticSearchDemoShared;

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
// - A similarity threshold KINDA works with e5-base-v2, though it's not ideal, and something like a reranker step would
//   be better for removing poor results when the vector search "closest" results are the "least-bad" (but ALL bad)
const string queryPrefix = "query:";
const string passagePrefix = "passage:";
const float defaultSimilarityThreshold = 0.8f;

var (searchIndex, searchIndexStartupError) = await SearchIndex.Load(modelFilePath, vocabFilePath, vectorisedChunksCacheFilePath, queryPrefix, passagePrefix, defaultSimilarityThreshold);
if (searchIndexStartupError is not null)
{
    searchIndexStartupError = $"Failure.. have you run the {nameof(GenerateSimilarityEmbeddings)} project first, to build the embeddings data?\n\n{searchIndexStartupError}";
    Console.WriteLine(searchIndexStartupError);
}

var rerankedSearch = searchIndex is null
    ? null
    : new RerankedSearch(
        searchIndex,
        ZeroReranker.Instance,
        (await BlogPostReader.Read(new DirectoryInfo(blogPostsFolderPath).EnumerateFiles("*.txt")).ToArrayAsync()).ToDictionary(post => post.Id));

var app = WebApplication.CreateBuilder(args).Build();

app.UseStaticFiles(); // Allow  favicon!

app.MapGet("/", async (HttpContext context) =>
{
    if (rerankedSearch is null)
    {
        return Results.Content(searchIndexStartupError ?? "Unknown Initialisation Error", statusCode: 500);
    }

    var query = context.Request.Query["q"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(query))
    {
        return context.Request.HasJsonContentType()
            ? Results.BadRequest("No 'q' query string value to search for")
            : Results.Content("<form><input name=q autofocus><input type=submit value=Search></form>", MediaTypeNames.Text.Html);
    }

    var logger = new TimingConsoleLogger();
    var results = await rerankedSearch.Search(query, maxNumberOfResults: 10, customRerankerThreshold: null, logger.Log, context.RequestAborted);

    if (context.Request.HasJsonContentType())
    {
        // Note: The JsonSerializer is bad with tuples, so transform the results array items to anonymous types
        return Results.Json(new {
            query,
            results = results.Select(result => new { result.Post.Id, result.Score, result.Excerpt }) });
    }

    var resultsContent = results.Count == 0
        ? "No results."
        : string.Join("\n\n", results.Select(result => $"Post {result.Post.Id} (semantic similarity score {result.Score:N5})\n{result.Excerpt}"));

    return Results.Content($"Searching for: {query}\n\n{resultsContent}");
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