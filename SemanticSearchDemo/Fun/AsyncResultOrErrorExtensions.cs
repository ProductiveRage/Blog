using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SemanticSearchDemo.Fun;

internal static class AsyncResultOrErrorExtensions
{
    public static async Task<ResultOrError<T2>> Map<T, T2>(this Task<ResultOrError<T>> source, Func<T, T2> map) =>
        (await source).Map(map);

    public static async Task<ResultOrError<T>> MapError<T>(this Task<ResultOrError<T>> source, Func<Error, Error> map) =>
        (await source).MapError(map);

    public static async Task<T> MapErrorToResult<T>(this Task<ResultOrError<T>> source, Func<Error, T> map) =>
        (await source).Match(
            result => result,
            error => map(error));

    public static async Task<ResultOrError<T2>> Bind<T, T2>(this Task<ResultOrError<T>> source, Func<T, ResultOrError<T2>> map) =>
        (await source).Bind(map);

    public static async Task<ResultOrError<T2>> Bind<T, T2>(this Task<ResultOrError<T>> source, Func<T, Task<ResultOrError<T2>>> map) =>
        await (await source).Bind(map);

    public static async Task<ResultOrError<T>> IfError<T>(this Task<ResultOrError<T>> source, Action<Error> callback) =>
        (await source).IfError(callback);
}