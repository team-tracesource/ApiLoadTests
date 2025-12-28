using TraceSource.LoadTests.Metrics;
using TraceSource.LoadTests.Models;
using TraceSource.LoadTests.Services;

namespace TraceSource.LoadTests;

public class LoadTestRunner(LoadTestConfig config)
{
    private readonly MetricsCollector _metrics = new();

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine(
            "╔════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                    TraceSource API Load Test                               ║"
        );
        Console.WriteLine(
            "╚════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();
        Console.WriteLine($"API Base URL: {config.Api.BaseUrl}");
        Console.WriteLine($"Iterations per user per phase: {config.LoadTest.IterationsPerUser}");
        Console.WriteLine($"Number of phases: {config.LoadTest.Phases.Count}");
        Console.WriteLine();

        using var database = new DatabaseService(
            config.MongoDB.ConnectionString,
            config.MongoDB.DatabaseName
        );

        // Cleanup any leftover test users from previous runs
        Console.WriteLine("Cleaning up previous test data...");
        await database.CleanupAllTestUsersAsync(@"test\+loadtest\.user.*@yopmail\.com", ct);
        Console.WriteLine();

        // Run each phase
        for (int phaseIndex = 0; phaseIndex < config.LoadTest.Phases.Count; phaseIndex++)
        {
            if (ct.IsCancellationRequested)
                break;

            var phase = config.LoadTest.Phases[phaseIndex];
            var phaseName = $"Phase {phaseIndex + 1} ({phase.Users} users)";

            await RunPhaseAsync(phaseName, phase, database, ct);

            // Wait before next phase (unless it's the last one)
            if (phaseIndex < config.LoadTest.Phases.Count - 1 && !ct.IsCancellationRequested)
            {
                Console.WriteLine($"\nWaiting 30 seconds before next phase...\n");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }

        // Rest period
        if (!ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine($"Resting for {config.LoadTest.RestDurationMinutes} minutes...");
            await Task.Delay(TimeSpan.FromMinutes(config.LoadTest.RestDurationMinutes), ct);
        }

        // Final cleanup
        Console.WriteLine("\nFinal cleanup...");
        await database.CleanupAllTestUsersAsync(@"test\+loadtest\.user.*@yopmail\.com", ct);

        // Print final report
        _metrics.PrintFinalReport();

        // Save detailed report
        var reportPath = Path.Combine(
            AppContext.BaseDirectory,
            $"load-test-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md"
        );
        await _metrics.SaveReportAsync(reportPath);
    }

    private async Task RunPhaseAsync(
        string phaseName,
        PhaseConfig phaseConfig,
        DatabaseService database,
        CancellationToken ct
    )
    {
        _metrics.StartPhase(phaseName, phaseConfig.Users);

        using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        phaseCts.CancelAfter(TimeSpan.FromMinutes(phaseConfig.DurationMinutes));

        var tasks = new List<Task>();

        for (int userId = 1; userId <= phaseConfig.Users; userId++)
        {
            var scenario = new UserScenario(
                config.Api.BaseUrl,
                _metrics,
                database,
                phaseName,
                config.LoadTest.IterationsPerUser
            );

            var userIdCopy = userId;
            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            await scenario.RunAsync(userIdCopy, phaseCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Phase time limit reached
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"  [User {userIdCopy}] Unexpected error: {ex.Message}"
                            );
                        }
                    },
                    ct
                )
            );

            // Stagger user starts to avoid thundering herd
            if (userId < phaseConfig.Users)
            {
                var staggerDelay = Math.Max(50, 1000 / phaseConfig.Users);
                await Task.Delay(staggerDelay, ct);
            }
        }

        // Wait for all users to complete or phase timeout
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n[{phaseName}] Phase time limit reached, stopping users...");
        }

        _metrics.EndPhase(phaseName);
    }
}
