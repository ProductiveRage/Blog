namespace SemanticSearchDemo.Fun;

internal static class ResultOrErrorHelpers
{
    public static async Task<ResultOrError<T>> Try<T>(Func<Task<T>> work)
    {
        try
        {
            return await work();
        }
        catch (Exception e)
        {
            return new Error(e.Message);
        }
    }
}