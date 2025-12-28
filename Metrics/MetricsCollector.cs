using System.Collections.Concurrent;

namespace TraceSource.LoadTests.Metrics;

public class MetricsCollector
{
    private readonly ConcurrentBag<RequestMetric> _metrics = [];
    private readonly ConcurrentDictionary<string, PhaseMetrics> _phaseMetrics = new();
    private readonly DateTime _testStartTime = DateTime.UtcNow;

    public void RecordRequest(
        string phase,
        string endpoint,
        string method,
        int statusCode,
        long latencyMs,
        bool isSuccess,
        string? errorMessage = null
    )
    {
        var metric = new RequestMetric
        {
            Phase = phase,
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow,
        };

        _metrics.Add(metric);

        // Update phase metrics
        _phaseMetrics.AddOrUpdate(
            phase,
            _ => new PhaseMetrics { Phase = phase },
            (_, existing) => existing
        );

        var phaseMetric = _phaseMetrics[phase];
        phaseMetric.AddRequest(metric);
    }

    public void StartPhase(string phase, int userCount)
    {
        _phaseMetrics.AddOrUpdate(
            phase,
            _ => new PhaseMetrics
            {
                Phase = phase,
                UserCount = userCount,
                StartTime = DateTime.UtcNow,
            },
            (_, existing) =>
            {
                existing.UserCount = userCount;
                existing.StartTime = DateTime.UtcNow;
                return existing;
            }
        );

        Console.WriteLine();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine(
            $"[{DateTime.UtcNow:HH:mm:ss}] STARTING PHASE: {phase} with {userCount} concurrent users"
        );
        Console.WriteLine(new string('=', 80));
    }

    public void EndPhase(string phase)
    {
        if (_phaseMetrics.TryGetValue(phase, out var phaseMetric))
        {
            phaseMetric.EndTime = DateTime.UtcNow;
            PrintPhaseReport(phaseMetric);
        }
    }

    private void PrintPhaseReport(PhaseMetrics phase)
    {
        Console.WriteLine();
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"PHASE COMPLETED: {phase.Phase}");
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"  Duration: {phase.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"  Concurrent Users: {phase.UserCount}");
        Console.WriteLine($"  Total Requests: {phase.TotalRequests}");
        Console.WriteLine($"  Successful: {phase.SuccessfulRequests} ({phase.SuccessRate:F2}%)");
        Console.WriteLine($"  Failed: {phase.FailedRequests}");
        Console.WriteLine($"  Avg Latency: {phase.AverageLatencyMs:F2}ms");
        Console.WriteLine($"  Min Latency: {phase.MinLatencyMs}ms");
        Console.WriteLine($"  Max Latency: {phase.MaxLatencyMs}ms");
        Console.WriteLine($"  P50 Latency: {phase.P50LatencyMs:F2}ms");
        Console.WriteLine($"  P95 Latency: {phase.P95LatencyMs:F2}ms");
        Console.WriteLine($"  P99 Latency: {phase.P99LatencyMs:F2}ms");
        Console.WriteLine($"  Requests/sec: {phase.RequestsPerSecond:F2}");

        // Breakdown by endpoint
        Console.WriteLine();
        Console.WriteLine("  Breakdown by Endpoint:");
        foreach (var endpoint in phase.GetEndpointBreakdown())
        {
            Console.WriteLine(
                $"    {endpoint.Key}: {endpoint.Value.Total} requests, "
                    + $"{endpoint.Value.SuccessRate:F1}% success, "
                    + $"avg {endpoint.Value.AvgLatency:F0}ms"
            );
        }

        Console.WriteLine(new string('-', 80));
    }

    public void PrintFinalReport()
    {
        var totalDuration = DateTime.UtcNow - _testStartTime;
        var allMetrics = _metrics.ToList();

        Console.WriteLine();
        Console.WriteLine(new string('*', 80));
        Console.WriteLine("FINAL LOAD TEST REPORT");
        Console.WriteLine(new string('*', 80));
        Console.WriteLine($"  Test Duration: {totalDuration:hh\\:mm\\:ss}");
        Console.WriteLine($"  Total Requests: {allMetrics.Count}");
        Console.WriteLine($"  Total Successful: {allMetrics.Count(m => m.IsSuccess)}");
        Console.WriteLine($"  Total Failed: {allMetrics.Count(m => !m.IsSuccess)}");
        Console.WriteLine(
            $"  Overall Success Rate: {(allMetrics.Count > 0 ? (double)allMetrics.Count(m => m.IsSuccess) / allMetrics.Count * 100 : 0):F2}%"
        );

        if (allMetrics.Count > 0)
        {
            var latencies = allMetrics.Select(m => m.LatencyMs).OrderBy(l => l).ToList();
            Console.WriteLine($"  Overall Avg Latency: {latencies.Average():F2}ms");
            Console.WriteLine($"  Overall P95 Latency: {GetPercentile(latencies, 95):F2}ms");
            Console.WriteLine($"  Overall P99 Latency: {GetPercentile(latencies, 99):F2}ms");
        }

        Console.WriteLine();
        Console.WriteLine("Phase Summary:");
        foreach (var phase in _phaseMetrics.Values.OrderBy(p => p.StartTime))
        {
            Console.WriteLine(
                $"  {phase.Phase}: {phase.TotalRequests} requests, "
                    + $"{phase.SuccessRate:F1}% success, "
                    + $"{phase.RequestsPerSecond:F1} req/s"
            );
        }

        Console.WriteLine(new string('*', 80));
    }

    public async Task SaveReportAsync(string filePath)
    {
        var report = GenerateDetailedReport();
        await File.WriteAllTextAsync(filePath, report);
        Console.WriteLine($"\nDetailed report saved to: {filePath}");
    }

    private string GenerateDetailedReport()
    {
        var sb = new System.Text.StringBuilder();
        var allMetrics = _metrics.ToList();
        var totalDuration = DateTime.UtcNow - _testStartTime;

        sb.AppendLine("# TraceSource API Load Test Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- **Test Duration**: {totalDuration:hh\\:mm\\:ss}");
        sb.AppendLine($"- **Total Requests**: {allMetrics.Count:N0}");
        sb.AppendLine($"- **Successful Requests**: {allMetrics.Count(m => m.IsSuccess):N0}");
        sb.AppendLine($"- **Failed Requests**: {allMetrics.Count(m => !m.IsSuccess):N0}");
        sb.AppendLine(
            $"- **Success Rate**: {(allMetrics.Count > 0 ? (double)allMetrics.Count(m => m.IsSuccess) / allMetrics.Count * 100 : 0):F2}%"
        );

        if (allMetrics.Count > 0)
        {
            var latencies = allMetrics.Select(m => m.LatencyMs).OrderBy(l => l).ToList();
            sb.AppendLine();
            sb.AppendLine("## Latency Statistics");
            sb.AppendLine($"- **Average**: {latencies.Average():F2}ms");
            sb.AppendLine($"- **Minimum**: {latencies.First()}ms");
            sb.AppendLine($"- **Maximum**: {latencies.Last()}ms");
            sb.AppendLine($"- **P50**: {GetPercentile(latencies, 50):F2}ms");
            sb.AppendLine($"- **P90**: {GetPercentile(latencies, 90):F2}ms");
            sb.AppendLine($"- **P95**: {GetPercentile(latencies, 95):F2}ms");
            sb.AppendLine($"- **P99**: {GetPercentile(latencies, 99):F2}ms");
        }

        sb.AppendLine();
        sb.AppendLine("## Phase Details");
        foreach (var phase in _phaseMetrics.Values.OrderBy(p => p.StartTime))
        {
            sb.AppendLine($"### {phase.Phase}");
            sb.AppendLine($"- **Concurrent Users**: {phase.UserCount}");
            sb.AppendLine($"- **Duration**: {phase.Duration:hh\\:mm\\:ss}");
            sb.AppendLine($"- **Total Requests**: {phase.TotalRequests:N0}");
            sb.AppendLine($"- **Success Rate**: {phase.SuccessRate:F2}%");
            sb.AppendLine($"- **Requests/sec**: {phase.RequestsPerSecond:F2}");
            sb.AppendLine($"- **Avg Latency**: {phase.AverageLatencyMs:F2}ms");
            sb.AppendLine($"- **P95 Latency**: {phase.P95LatencyMs:F2}ms");
            sb.AppendLine();

            sb.AppendLine("| Endpoint | Requests | Success % | Avg Latency |");
            sb.AppendLine("|----------|----------|-----------|-------------|");
            foreach (var endpoint in phase.GetEndpointBreakdown())
            {
                sb.AppendLine(
                    $"| {endpoint.Key} | {endpoint.Value.Total} | {endpoint.Value.SuccessRate:F1}% | {endpoint.Value.AvgLatency:F0}ms |"
                );
            }

            sb.AppendLine();
        }

        // Error summary
        var errors = allMetrics.Where(m => !m.IsSuccess).GroupBy(m => m.ErrorMessage ?? "Unknown");
        if (errors.Any())
        {
            sb.AppendLine("## Error Summary");
            foreach (var errorGroup in errors.OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"- **{errorGroup.Key}**: {errorGroup.Count()} occurrences");
            }
        }

        return sb.ToString();
    }

    private static double GetPercentile(List<long> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
            return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }
}

public class RequestMetric
{
    public required string Phase { get; init; }
    public required string Endpoint { get; init; }
    public required string Method { get; init; }
    public required int StatusCode { get; init; }
    public required long LatencyMs { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime Timestamp { get; init; }
}

public class PhaseMetrics
{
    public required string Phase { get; init; }
    public int UserCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    private readonly ConcurrentBag<RequestMetric> _requests = [];

    public void AddRequest(RequestMetric metric) => _requests.Add(metric);

    /// <summary>
    /// Normalizes endpoints with IDs to a template form for grouping.
    /// e.g., "/api/v1/forms/abc123" becomes "/api/v1/forms/{id}"
    /// </summary>
    private static string NormalizeEndpoint(string endpoint)
    {
        // Pattern: /api/v1/forms/{id} - match UUID or MongoDB ObjectId patterns
        var patterns = new (string pattern, string replacement)[]
        {
            (@"/api/v1/forms/[a-fA-F0-9]{24}$", "/api/v1/forms/{id}"),
            (@"/api/v1/forms/[a-fA-F0-9-]{36}$", "/api/v1/forms/{id}"),
            (@"/api/v1/forms/[a-zA-Z0-9]+$", "/api/v1/forms/{id}"),
            (@"/api/v1/organizations/[a-fA-F0-9]{24}$", "/api/v1/organizations/{id}"),
            (@"/api/v1/organizations/[a-fA-F0-9-]{36}$", "/api/v1/organizations/{id}"),
            (@"/api/v1/organizations/[a-zA-Z0-9]+$", "/api/v1/organizations/{id}"),
        };

        foreach (var (pattern, replacement) in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(endpoint, pattern))
            {
                return replacement;
            }
        }

        return endpoint;
    }

    public int TotalRequests => _requests.Count;
    public int SuccessfulRequests => _requests.Count(r => r.IsSuccess);
    public int FailedRequests => _requests.Count(r => !r.IsSuccess);
    public double SuccessRate =>
        TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
    public TimeSpan Duration => EndTime - StartTime;
    public double RequestsPerSecond =>
        Duration.TotalSeconds > 0 ? TotalRequests / Duration.TotalSeconds : 0;

    public double AverageLatencyMs => _requests.Count > 0 ? _requests.Average(r => r.LatencyMs) : 0;

    public long MinLatencyMs => _requests.Count > 0 ? _requests.Min(r => r.LatencyMs) : 0;
    public long MaxLatencyMs => _requests.Count > 0 ? _requests.Max(r => r.LatencyMs) : 0;

    public double P50LatencyMs => GetPercentile(50);
    public double P95LatencyMs => GetPercentile(95);
    public double P99LatencyMs => GetPercentile(99);

    private double GetPercentile(int percentile)
    {
        var sorted = _requests.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        if (sorted.Count == 0)
            return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    public Dictionary<string, EndpointMetrics> GetEndpointBreakdown()
    {
        return _requests
            .GroupBy(r => $"{r.Method} {NormalizeEndpoint(r.Endpoint)}")
            .ToDictionary(
                g => g.Key,
                g => new EndpointMetrics
                {
                    Total = g.Count(),
                    Successful = g.Count(r => r.IsSuccess),
                    AvgLatency = g.Average(r => r.LatencyMs),
                }
            );
    }
}

public class EndpointMetrics
{
    public int Total { get; init; }
    public int Successful { get; init; }
    public double AvgLatency { get; init; }
    public double SuccessRate => Total > 0 ? (double)Successful / Total * 100 : 0;
}
