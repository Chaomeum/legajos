using Azure;
using Azure.Data.Tables;

namespace AF.Infrastructure.Storage;

/*
* 
*/
public class ConfigMappingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // clientType
    public string RowKey { get; set; } = default!; // regulationScope

    public string? ClientType { get; set; }
    public string? RegulationScope { get; set; }
    public string? RoutingProfile { get; set; }
    public string? ClassifierId { get; set; }
    public string? ExtractionModelMapJson { get; set; }
    public string? RequiredDocumentFamilies { get; set; }
    public double ConfidenceThreshold { get; set; }
    public bool IsActive { get; set; }
    public string? Version { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
