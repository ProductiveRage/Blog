using SemanticSearchDemo;
using SemanticSearchDemo.EndToEndSearch;
using SemanticSearchDemo.Fun;

var rerankedSearch = await Initialisation.LoadRerankedSearch(new TimingConsoleLogger().Log);

var app = WebApplication.CreateBuilder(args).Build();

app.UseStaticFiles(); // Allow  favicon!

app.MapGet("/", async (HttpContext context, CancellationToken cancellationToken) => await rerankedSearch.PerformSearch(context.Request, cancellationToken));

app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == 404 && !context.Response.HasStarted)
    {
        await context.Response.WriteAsync("Page not found.");
    }
});

app.Run();