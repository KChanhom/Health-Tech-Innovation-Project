using Hl7.Fhir.Model;

namespace ProcessingService.Validation;

/// <summary>
/// Result of a FHIR resource validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult Failure(params ValidationIssue[] issues) =>
        new() { IsValid = false, Issues = issues.ToList() };
}

public class ValidationIssue
{
    public string Severity { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public string? Location { get; set; }

    public override string ToString() => $"[{Severity}] {Message}" + (Location != null ? $" at {Location}" : "");
}
