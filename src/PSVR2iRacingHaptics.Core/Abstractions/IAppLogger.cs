namespace PSVR2iRacingHaptics.Core.Abstractions;

public interface IAppLogger
{
    event EventHandler<string>? LineWritten;
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();
    public event EventHandler<string>? LineWritten
    {
        add { }
        remove { }
    }
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
}
