namespace AF.Domain.Validation;

public class ValidationResult
{
    public string CriterionId { get; set; } = default!;
    public string Description { get; set; } = default!;

    public string Status { get; set; } = default!; // OK | NOK | NA
    public string? Evidence { get; set; }
    public string? Observation { get; set; }
}