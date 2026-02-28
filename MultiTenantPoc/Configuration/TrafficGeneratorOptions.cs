namespace MultiTenantPoc;

public sealed class TrafficGeneratorOptions
{
    public const string SectionName = "TrafficGenerator";

    public bool Enabled { get; init; } = true;
    public int StartupDelaySeconds { get; init; } = 5;
    public int MinDelayMs { get; init; } = 750;
    public int MaxDelayMs { get; init; } = 1500;
}
