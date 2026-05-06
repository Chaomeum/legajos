namespace AF.Infrastructure.Requests;

public class IntakeRequest
{
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string BlobPath { get; set; } = "";
    public string? Container { get; set; }
    public string? ClientType { get; set; }
    public string? TenantId { get; set; }
    public string? RegulationScope { get; set; }
    public string CaseId { get; set; } = "";
    public string SampleId { get; set; } = "";
    public string? PersonType { get; set; }
    public string? PersonSubType { get; set; }
    public string? Regime { get; set; }
}