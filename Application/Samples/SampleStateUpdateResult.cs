namespace AF.Application.Samples;

public sealed record SampleStateUpdateResult(
    string SampleStatus,
    IReadOnlyList<string> ExpectedDocumentTypes,
    IReadOnlyList<string> ReceivedDocumentTypes
);