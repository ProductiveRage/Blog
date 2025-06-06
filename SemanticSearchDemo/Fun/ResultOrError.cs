namespace SemanticSearchDemo.Fun;

public sealed class ResultOrError<T>
{
    private readonly T? _result;
    private readonly string? _error;

    private ResultOrError(T? result, string? error)
    {
        _result = result;
        _error = error;
    }

    public ResultOrError<T2> Map<T2>(Func<T, T2> map) =>
        _error is null
            ? new(result: map(_result!), error: null)
            : FromError<T2>(_error);

    public async Task<ResultOrError<T2>> Map<T2>(Func<T, Task<T2>> map) =>
        _error is null
            ? new(result: await map(_result!), error: null)
            : FromError<T2>(_error);

    public ResultOrError<T> MapError(Func<string, string> map) =>
        _error is null
            ? this
            : FromError(map(_error));

    public ResultOrError<T2> Bind<T2>(Func<T, ResultOrError<T2>> map) =>
        _error is null
            ? map(_result!)
            : FromError<T2>(_error);

    public async Task<ResultOrError<T2>> Bind<T2>(Func<T, Task<ResultOrError<T2>>> map) =>
        _error is null
            ? await map(_result!)
            : FromError<T2>(_error);

    public async Task<T2> Match<T2>(Func<T, Task<T2>> result, Func<string, T2> error) =>
        _error is null
            ? await result(_result!)
            : error(_error);

    public ResultOrError<T> IfError(Action<string> callback)
    {
        if (_error is not null)
        {
            callback(_error);
        }
        return this;
    }

    public static ResultOrError<T> FromError(string error) => new(result: default, error);

    public static implicit operator ResultOrError<T>(T value) => new(value, error: null);

    private static ResultOrError<T2> FromError<T2>(string error) => new(result: default, error);
}