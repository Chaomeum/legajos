namespace AF.Domain.Samples;

public sealed record SampleStateRecord
{
    public string CaseId { get; init; } = default!;
    public string SampleId { get; init; } = default!;
    public string Status { get; init; } = "Partial";
    public IReadOnlyList<string> ExpectedDocumentTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReceivedDocumentTypes { get; init; } = Array.Empty<string>();
    public string? PersonType { get; init; }
    public string? PersonSubType { get; init; }
    public string? Regime { get; init; }
    public string? Regulation { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? Version { get; init; }
}