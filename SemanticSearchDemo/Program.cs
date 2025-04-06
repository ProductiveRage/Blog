using System.Net.Mime;
using System.Reflection;
using SemanticSearchDemo;

var outputFolderPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;
var modelFilePath = Path.Combine(outputFolderPath, "embedding model.onnx");
var vocabFilePath = Path.Combine(outputFolderPath, "embedding model vocab.txt");
var vectorisedChunksCacheFilePath = Path.Combine(outputFolderPath, "embeddings.bin");

Console.WriteLine("Loading search data..");
var (searchIndex, startupError) = await SearchIndex.Load(modelFilePath, vocabFilePath, vectorisedChunksCacheFilePath);
if (startupError is not null)
{
    startupError = $"Failure.. have you run the {nameof(GenerateSimilarityEmbeddings)} project first, to build the embeddings data?\n\n{startupError}";
    Console.WriteLine(startupError);
}

var app = WebApplication.CreateBuilder(args).Build();

app.UseStaticFiles(); // Allow  favicon!

app.MapGet("/", async (HttpContext context) =>
{
    if (searchIndex is null)
    {
        return Results.Content(startupError ?? "Unknown Initialisation Error", statusCode: 500);
    }

    var query = context.Request.Query["q"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(query))
    {
        return context.Request.HasJsonContentType()
            ? Results.BadRequest("No 'q' query string value to search for")
            : Results.Content("<form><input name=q autofocus><input type=submit value=Search></form>", MediaTypeNames.Text.Html);
    }

    var results = await searchIndex.Search(query);

    if (context.Request.HasJsonContentType())
    {
        // Note: The JsonSerializer is bad with tuples, so transform the results array items to anonymous types
        return Results.Json(new {
            query,
            results = results.Select(result => new { result.PostId, result.Score, result.Excerpt }) });
    }

    var resultsContent = results.Count == 0
        ? "No results."
        : string.Join("\n\n", results.Select(result => $"Post {result.PostId} (semantic similarity score {result.Score:N5})\n{result.Excerpt}"));

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