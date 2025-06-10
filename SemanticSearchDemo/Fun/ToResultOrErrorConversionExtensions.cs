namespace SemanticSearchDemo.Fun;

internal static class ToResultOrErrorConversionExtensions
{
    public static ResultOrError<T> ToResultOrError<T>(this T? value, Func<Error> ifNull) =>
        value is null
            ? ifNull()
            : value!;
}