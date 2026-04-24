namespace AF.Infrastructure.Requests;

using AF.Domain.Export;

public class ExportExcelRequest
{
    public string CaseId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string ClientType { get; set; } = default!;
    public string Regulation { get; set; } = default!;
    public string PersonType { get; set; } = default!;
    public string PersonSubType { get; set; } = default!;

    public List<ExportSample> Samples { get; set; } = new();
}
