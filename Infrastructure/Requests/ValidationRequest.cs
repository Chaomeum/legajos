namespace AF.Infrastructure.Requests;

using AF.Domain.Documents;

public class ValidationRequest
{
    public string CaseId { get; set; } = default!;
    public string SampleId { get; set; } = default!;

    public string Regulation { get; set; } = default!;      // SBS, SMV
    public string PersonType { get; set; } = default!;      // Clientes
    public string PersonSubType { get; set; } = default!;   // PN / PJ
    public string Regime { get; set; } = "Estandar";

    public List<CanonicalExtractionResult> ExtractedDocuments { get; set; } = new();
}