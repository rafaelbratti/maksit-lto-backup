using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.Logging;

[ProviderAlias("FileLogger")]
public class FileLoggerProvider : ILoggerProvider {
  private readonly string _filePath;

  public FileLoggerProvider(string filePath) {
    _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
  }

  public ILogger CreateLogger(string categoryName) {
    return new FileLogger(_filePath);
  }

  public void Dispose() { }
}
