namespace AF.Infrastructure.Requests;

public class IntakeRequest
{
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? EventType { get; set; }
    public string? ClientType { get; set; }
    public string? RegulationScope { get; set; }
    public string? BlobPath { get; set; }
    public string? Container { get; set; }
    public string? TenantId { get; set; }
}