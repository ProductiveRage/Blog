namespace SemanticSearchDemo.Fun;

internal sealed record Error(string Message)
{
    public override string ToString() => Message;

    public static implicit operator Error(string message) => new(message);
}