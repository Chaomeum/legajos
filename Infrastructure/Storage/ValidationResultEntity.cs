using Azure;
using Azure.Data.Tables;

namespace AF.Infrastructure.Storage;

public class ValidationResultEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // CaseId
    public string RowKey { get; set; } = default!;       // SampleId|CriterionId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string CaseId { get; set; } = default!;
    public string SampleId { get; set; } = default!;
    public string CriterionId { get; set; } = default!;
    public string Status { get; set; } = default!;       // OK | NOK | NA
    public string? Evidence { get; set; }
    public string? Observation { get; set; }

    public DateTimeOffset ValidatedAt { get; set; }
}