using DotNetEnv;
using Microsoft.Extensions.Configuration;
using TraceSource.LoadTests;
using TraceSource.LoadTests.Models;

// Load environment variables from .env file (if exists)
Env.Load();

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Parse configuration
var config = new LoadTestConfig
{
    Api = new ApiConfig
    {
        BaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000",
    },
    MongoDB = new MongoConfig
    {
        ConnectionString =
            Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("MongoDB connection string is required"),
        DatabaseName =
            Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") ?? "TraceSourceDB",
    },
    LoadTest = new LoadTestSettings
    {
        Phases =
            configuration.GetSection("LoadTest:Phases").Get<List<PhaseConfig>>()
            ??
            [
                new PhaseConfig { Users = 20, DurationMinutes = 10 },
                new PhaseConfig { Users = 50, DurationMinutes = 10 },
                new PhaseConfig { Users = 100, DurationMinutes = 10 },
                new PhaseConfig { Users = 300, DurationMinutes = 10 },
            ],
        IterationsPerUser = configuration.GetValue("LoadTest:IterationsPerUser", 10),
        RestDurationMinutes = configuration.GetValue("LoadTest:RestDurationMinutes", 10),
    },
};

// Handle Ctrl+C gracefully
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nCancellation requested, stopping gracefully...");
    cts.Cancel();
};

// Run the load test
var runner = new LoadTestRunner(config);

try
{
    await runner.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nLoad test cancelled.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nLoad test failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

Console.WriteLine("\nLoad test completed.");
