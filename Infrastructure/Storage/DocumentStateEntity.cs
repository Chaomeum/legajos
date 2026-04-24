using Azure;
using Azure.Data.Tables;

namespace AF.Infrastructure.Storage;

/*
* 
*/
public class DocumentStateEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // tenantId or clientType
    public string RowKey { get; set; } = default!; // documentId

    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? FileUrl { get; set; }
    public string? ClientType { get; set; }
    public string? RegulationScope { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public string? ClassifierId { get; set; }
    public string? AllowedExtractionModels { get; set; } //ExtractionModelId
    public string? RoutingProfile { get; set; }
    public string? DetectedType { get; set; }
    public double ConfidenceThreshold { get; set; }
    public double ClassificationConfidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
