namespace SemanticSearchDemo.Fun;

internal static class ResultOrError
{
    /// <summary>
    /// This static method allows type inference to save the calling code from specifying the type
    /// </summary>
    public static ResultOrError<T> FromResult<T>(T result) => ResultOrError<T>.FromResult(result);
}

internal sealed class ResultOrError<T>
{
    private readonly T? _result;
    private readonly Error? _error;

    private ResultOrError(T? result, Error? error)
    {
        _result = result;
        _error = error;
    }

    public ResultOrError<T2> Map<T2>(Func<T, T2> map) =>
        _error is null
            ? new ResultOrError<T2>(result: map(_result!), error: null)
            : _error;

    public ResultOrError<T> MapError(Func<Error, Error> map) =>
        _error is null
            ? this
            : map(_error);

    public ResultOrError<T2> Bind<T2>(Func<T, ResultOrError<T2>> map) =>
        _error is null
            ? map(_result!)
            : _error;

    public async Task<ResultOrError<T2>> Bind<T2>(Func<T, Task<ResultOrError<T2>>> map) =>
        _error is null
            ? await map(_result!)
            : _error;

    public async Task<ResultOrError<T2>> Bind<T2>(Func<T, Task<T2>> map) =>
        _error is null
            ? new ResultOrError<T2>(result: await map(_result!), error: null)
            : _error;

    public T2 Match<T2>(Func<T, T2> result, Func<Error, T2> error) =>
        _error is null
            ? result(_result!)
            : error(_error);

    public ResultOrError<T> IfError(Action<Error> callback)
    {
        if (_error is not null)
        {
            callback(_error);
        }
        return this;
    }

    /// <summary>
    /// If the result type is an interface then you can't use the implicit operator, and a method is needed to create an instance for a result
    /// ('user-defined conversions to or from an interface are not allowed`)
    /// </summary>
    public static ResultOrError<T> FromResult(T result) => new(result, error: null);

    public static implicit operator ResultOrError<T>(T value) => new(value, error: null);

    public static implicit operator ResultOrError<T>(Error error) => new(result: default, error);
}