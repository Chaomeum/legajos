using Azure; 
using Azure.Data.Tables;

namespace AF.Infrastructure.Storage;

public class SampleEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // CaseId
    public string RowKey { get; set; } = default!;       // SampleId (GUID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string CaseId { get; set; } = default!;
    public string SampleId { get; set; } = default!;     // GUID (string)
    public string Status { get; set; } = default!;       // Draft | Partial | Ready | Validated
    public string ExpectedDocumentTypes { get; set; } = default!;   // ["DNI","CUL"]
    public string ReceivedDocumentTypes { get; set; } = default!;   // ["DNI"]
    public string PersonType { get; set; } = default!;
    public string PersonSubType { get; set; } = default!;
    public string Regime { get; set; } = default!;
    public string Regulation { get; set; } = default!;

    public DateTimeOffset UpdatedAt { get; set; }
}
