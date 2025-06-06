namespace SemanticSearchDemo.Fun;

public static class ResultOrErrorExtensions
{
    public static async Task<ResultOrError<T2>> Map<T, T2>(this Task<ResultOrError<T>> source, Func<T, T2> map) =>
        (await source).Map(map);
}