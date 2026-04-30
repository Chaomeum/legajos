using Azure;
using Azure.Data.Tables;
using System;

namespace AF.Infrastructure.Storage;

public class SampleDocumentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // CaseId
    public string RowKey { get; set; } = default!;       // SampleId|DocumentType|DocumentId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string CaseId { get; set; } = default!;
    public string SampleId { get; set; } = default!;     // GUID (string)
    public string DocumentType { get; set; } = default!;
    public string DocumentId { get; set; } = default!;
    public string Status { get; set; } = default!;       // Extracted | Failed
    public string CanonicalResult { get; set; } = default!;
    public string ExtractionModelId { get; set; } = default!;
    public double Confidence { get; set; }

    public string BlobPath { get; set; } = default!;
    public string FileName { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
}