using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MaksIT.LTO.Core.Logging;

namespace MaksIT.LTO.Backup;

class Program {

  public static void Main() {

    // Set up configuration with reload support
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("configuration.json", optional: false, reloadOnChange: true)  // Enable reload on change
        .Build();

    var serviceProvider = new ServiceCollection()
        .Configure<Configuration>(configuration.GetSection("Configuration")) // Bind AppConfig directly
        .AddSingleton(configuration) // Make IConfiguration available if needed
        .AddLogging(builder =>
        {
          builder.AddConfiguration(configuration.GetSection("Logging"));
          builder.AddConsole();
          builder.AddFile(Path.Combine(Directory.GetCurrentDirectory(), "log.txt"));
        })
        .AddTransient<Application>()
        .BuildServiceProvider();


    // Get the App service and run it
    var app = serviceProvider.GetRequiredService<Application>();
    app.Run();
  }
}
