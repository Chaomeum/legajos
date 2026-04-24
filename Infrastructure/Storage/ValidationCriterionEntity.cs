using Azure;
using Azure.Data.Tables;

namespace AF.Infrastructure.Storage;

public class ValidationCriterionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    public string Description { get; set; } = default!;
    public string ValidationType { get; set; } = default!; // DocumentExists, RequiredFields

    public string DocumentType { get; set; } = "Default";
    public string? RequiredDocumentTypes { get; set; }     // CSV
    public string? RequiredFields { get; set; }             // CSV

    public bool IsActive { get; set; }
    public string Phase { get; set; } = "Fase1";

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}