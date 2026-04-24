namespace AF.Domain.Export;

using AF.Domain.Documents;
using AF.Domain.Validation;

public class ExportSample
{
    public int SampleId { get; set; }
    public List<ValidationResult> ValidationResults { get; set; } = new();
    public List<CanonicalExtractionResult> ExtractedDocuments { get; set; } = new();
}