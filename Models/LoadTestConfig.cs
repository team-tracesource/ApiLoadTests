namespace TraceSource.LoadTests.Models;

public record LoadTestConfig
{
    public required ApiConfig Api { get; init; }
    public required MongoConfig MongoDB { get; init; }
    public required LoadTestSettings LoadTest { get; init; }
}

public record ApiConfig
{
    public required string BaseUrl { get; init; }
}

public record MongoConfig
{
    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
}

public record LoadTestSettings
{
    public required List<PhaseConfig> Phases { get; init; }
    public required int IterationsPerUser { get; init; }
    public required int RestDurationMinutes { get; init; }
}

public record PhaseConfig
{
    public required int Users { get; init; }
    public required int DurationMinutes { get; init; }
}
