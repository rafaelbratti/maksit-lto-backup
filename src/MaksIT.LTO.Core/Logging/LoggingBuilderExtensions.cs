using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.Logging;

public static class LoggingBuilderExtensions {
  public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath) {
    builder.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider(filePath));
    return builder;
  }
}
