using System.Text.Json;

namespace AF.Domain.Samples;

public sealed class SampleStateService
{
    public IReadOnlyList<string> ParseRequiredDocumentFamilies(string? requiredDocumentFamilies)
    {
        if (string.IsNullOrWhiteSpace(requiredDocumentFamilies))
            return Array.Empty<string>();

        var trimmed = requiredDocumentFamilies.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                return Normalize(parsed ?? Enumerable.Empty<string>());
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        return Normalize(trimmed.Split(
            new[] { ',', ';', '|' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        ));
    }

    public SampleStateRecord ApplyReceivedDocumentType(
        SampleStateRecord? existingSample,
        string caseId,
        string sampleId,
        IReadOnlyList<string> expectedDocumentTypes,
        string detectedType,
        string? personType,
        string? personSubType,
        string? regime,
        string? regulation
    )
    {
        var receiveDocumentTypes = Normalize(
            (existingSample?.ReceivedDocumentTypes ?? Array.Empty<string>()).Append(detectedType)
        );

        var status = IsComplete(expectedDocumentTypes, receiveDocumentTypes) ? "Ready" : "Partial";

        return new SampleStateRecord
        {
            CaseId = caseId,
            SampleId = sampleId,
            Status = status,
            ExpectedDocumentTypes = expectedDocumentTypes,
            ReceivedDocumentTypes = receiveDocumentTypes,
            PersonType = string.IsNullOrWhiteSpace(personType) ? existingSample?.PersonType : personType,
            PersonSubType = string.IsNullOrWhiteSpace(personSubType) ? existingSample?.PersonSubType : personSubType,
            Regime = string.IsNullOrWhiteSpace(regime) ? existingSample?.Regime : regime,
            Regulation = string.IsNullOrWhiteSpace(regulation) ? existingSample?.Regulation : regulation,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = existingSample?.Version
        };
    }

    private static bool IsComplete(IReadOnlyList<string> expectedDocumentTypes, IReadOnlyList<string> receiveDocumentTypes)
    {
        if (expectedDocumentTypes.Count == 0)
            return false;

        var received = new HashSet<string>(receiveDocumentTypes, StringComparer.OrdinalIgnoreCase);
        return expectedDocumentTypes.All(received.Contains);
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string> documentTypes)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var documentType in documentTypes)
        {
            if (string.IsNullOrWhiteSpace(documentType))
                continue;

            var normalized = documentType.Trim();
            if (seen.Add(normalized))
                result.Add(normalized);
        }
        return result;
    }
}