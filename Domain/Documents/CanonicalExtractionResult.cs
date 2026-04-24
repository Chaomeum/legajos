namespace AF.Domain.Documents;

public class CanonicalExtractionResult
{
    public string DocumentType { get; set; } = "Default";
    public string? ClientType { get; set; }
    public string? RegulationScope { get; set; }
    public string? ExtractionModelId { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}