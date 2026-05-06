namespace AF.Application.Samples;

public sealed record SampleStateUpdateCommand(
    string CaseId,
    string SampleId,
    string RequiredDocumentFamilies,
    string DetectedType,
    string? PersonType,
    string? PersonSubType,
    string? Regime,
    string? Regulation
);