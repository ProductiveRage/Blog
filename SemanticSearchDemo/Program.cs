using System.Net.Mime;
using System.Reflection;
using SemanticSearchDemo;

var outputFolderPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;
var modelFilePath = Path.Combine(outputFolderPath, "embedding  model.onnx");
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

app.MapGet("/", async (HttpContext context) =>
{
    if (searchIndex is null)
    {
        return Results.Content(startupError ?? "Unknown Initialisation Error", statusCode: 500);
    }

    var query = context.Request.Query["q"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.Content("<form><input name=q autofocus><input type=submit value=Search></form>", MediaTypeNames.Text.Html);
    }

    var results = await searchIndex.Search(query);
    var resultsContent = results.Count == 0
        ? "No results."
        : string.Join("\n\n", results.Select(result => $"Post {result.PostId} (semantic similarity score {result.Score:N5})\n{result.Excerpt}"));

    return Results.Content($"Searching for: {query}\n\n{resultsContent}");
});

app.Run();