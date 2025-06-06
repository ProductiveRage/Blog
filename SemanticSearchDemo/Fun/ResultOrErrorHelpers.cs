namespace SemanticSearchDemo.Fun;

public static class ResultOrErrorHelpers
{
    public static async ValueTask<ResultOrError<T>> Try<T>(Func<ValueTask<T>> work)
    {
        try
        {
            return await work();
        }
        catch (Exception e)
        {
            return ResultOrError<T>.FromError(e.Message);
        }
    }
}