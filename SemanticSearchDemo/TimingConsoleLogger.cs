using System.Diagnostics;

namespace SemanticSearchDemo;

internal sealed class TimingConsoleLogger
{
    private readonly Stopwatch _timer;
    
    public TimingConsoleLogger() => _timer = Stopwatch.StartNew();

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine(message);
            return;
        }

        Console.WriteLine($"[{_timer.Elapsed.TotalSeconds:0.00}s] {message}");
        _timer.Restart();
    }
}
