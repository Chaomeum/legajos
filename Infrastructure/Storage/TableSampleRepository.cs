using AF.Application.Samples;
using AF.Domain.Samples;
using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace AF.Infrastructure.Storage;

public sealed class TableSampleRepository : ISampleRepository
{
    private readonly TableClient _tableClient;

    public TableSampleRepository(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    public async Task<SampleStateRecord?> GetAsync(string caseId, string sampleId)
    {
        var response = await _tableClient.GetEntityIfExistsAsync<SampleEntity>(caseId, sampleId);
        return response.HasValue && response.Value is not null
            ? ToRecord(response.Value)
            : null;
    }

    public async Task<bool> TryCreateAsync(SampleStateRecord sample)
    {
        try
        {
            await _tableClient.AddEntityAsync(ToEntity(sample));
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(SampleStateRecord sample)
    {
        if (string.IsNullOrWhiteSpace(sample.Version))
            return false;

        try
        {
            var entity = ToEntity(sample);
            await _tableClient.UpdateEntityAsync(entity, new ETag(sample.Version), TableUpdateMode.Replace);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            return false;
        }
    }

    private static SampleStateRecord ToRecord(SampleEntity entity)
    {
        return new SampleStateRecord
        {
            CaseId = entity.CaseId,
            SampleId = entity.SampleId,
            Status = entity.Status,
            ExpectedDocumentTypes = DeserializeDocumentTypes(entity.ExpectedDocumentTypes),
            ReceivedDocumentTypes = DeserializeDocumentTypes(entity.ReceivedDocumentTypes),
            PersonType = entity.PersonType,
            PersonSubType = entity.PersonSubType,
            Regime = entity.Regime,
            Regulation = entity.Regulation,
            UpdatedAt = entity.UpdatedAt,
            Version = entity.ETag.ToString()
        };
    }

    private static SampleEntity ToEntity(SampleStateRecord sample)
    {
        return new SampleEntity
        {
            PartitionKey = sample.CaseId,
            RowKey = sample.SampleId,
            CaseId = sample.CaseId,
            SampleId = sample.SampleId,
            Status = sample.Status,
            ExpectedDocumentTypes = JsonSerializer.Serialize(sample.ExpectedDocumentTypes),
            ReceivedDocumentTypes = JsonSerializer.Serialize(sample.ReceivedDocumentTypes),
            PersonType = sample.PersonType ?? string.Empty,
            PersonSubType = sample.PersonSubType ?? string.Empty,
            Regime = sample.Regime ?? string.Empty,
            Regulation = sample.Regulation ?? string.Empty,
            UpdatedAt = sample.UpdatedAt,
            ETag = string.IsNullOrWhiteSpace(sample.Version) ? ETag.All : new ETag(sample.Version)
        };
    }

    private static IReadOnlyList<string> DeserializeDocumentTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(value);
            return parsed is null ? Array.Empty<string>() : parsed;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}