using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.Logging;

public class FileLogger : ILogger {
  private readonly string _filePath;
  private readonly object _lock = new object();

  public FileLogger(string filePath) {
    _filePath = filePath;
  }

  public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

  public bool IsEnabled(LogLevel logLevel) {
    return logLevel != LogLevel.None;
  }

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
    if (!IsEnabled(logLevel))
      return;

    var message = formatter(state, exception);
    if (string.IsNullOrEmpty(message))
      return;

    var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {message}";
    if (exception != null) {
      logRecord += Environment.NewLine + exception;
    }

    lock (_lock) {
      File.AppendAllText(_filePath, logRecord + Environment.NewLine);
    }
  }
}
