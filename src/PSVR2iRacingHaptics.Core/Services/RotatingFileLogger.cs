using System.Text;
using PSVR2iRacingHaptics.Core.Abstractions;

namespace PSVR2iRacingHaptics.Core.Services;

public sealed class RotatingFileLogger : IAppLogger, IDisposable
{
    private readonly object _sync = new();
    private readonly string _directory;
    private readonly long _maximumBytes;
    private readonly int _retainedFiles;
    private readonly string _baseName;
    private StreamWriter? _writer;
    private bool _disposed;

    public RotatingFileLogger(
        string directory,
        string baseName = "psvr2-iracing-haptics",
        long maximumBytes = 5 * 1024 * 1024,
        int retainedFiles = 4)
    {
        _directory = directory;
        _baseName = baseName;
        _maximumBytes = Math.Max(64 * 1024, maximumBytes);
        _retainedFiles = Math.Clamp(retainedFiles, 1, 20);
        Directory.CreateDirectory(_directory);
    }

    public event EventHandler<string>? LineWritten;

    public void Info(string message) => Write("INFO", message, null);
    public void Warning(string message) => Write("AVISO", message, null);
    public void Error(string message, Exception? exception = null) =>
        Write("ERRO", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var exceptionText = exception is null
            ? string.Empty
            : $" | {exception.GetType().Name}: {exception.Message}";
        var line =
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{exceptionText}";

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                EnsureWriter();
                _writer!.WriteLine(line);
                _writer.Flush();
            }
            catch
            {
                // Falha de log nunca deve encerrar a aplicação.
            }
        }

        LineWritten?.Invoke(this, line);
    }

    private void EnsureWriter()
    {
        var path = Path.Combine(_directory, _baseName + ".log");
        if (_writer is not null)
        {
            if (_writer.BaseStream.Length < _maximumBytes)
            {
                return;
            }

            _writer.Dispose();
            _writer = null;
            Rotate(path);
        }
        else if (File.Exists(path) && new FileInfo(path).Length >= _maximumBytes)
        {
            Rotate(path);
        }

        _writer = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    private void Rotate(string currentPath)
    {
        var oldest = currentPath + "." + _retainedFiles;
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = _retainedFiles - 1; index >= 1; index--)
        {
            var source = currentPath + "." + index;
            var destination = currentPath + "." + (index + 1);
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        if (File.Exists(currentPath))
        {
            File.Move(currentPath, currentPath + ".1", overwrite: true);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
