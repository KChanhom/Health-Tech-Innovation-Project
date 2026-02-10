using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Validation;
using Microsoft.Extensions.Logging;
using Shared.Fhir;
using Task = System.Threading.Tasks.Task;

namespace ProcessingService.Validation;

/// <summary>
/// Service for validating FHIR resources using Firely SDK validation
/// and optional server-side $validate operation.
/// </summary>
public class FhirValidationService
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<FhirValidationService> _logger;

    public FhirValidationService(IFhirCrudService fhirService, ILogger<FhirValidationService> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    /// <summary>
    /// Validates a FHIR resource locally using basic structural checks.
    /// </summary>
    public ValidationResult ValidateLocally(Resource resource)
    {
        _logger.LogInformation("Validating {ResourceType} locally...", resource.TypeName);

        var issues = new List<ValidationIssue>();

        // Check that the resource has a valid type
        if (string.IsNullOrEmpty(resource.TypeName))
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Resource type is missing"
            });
        }

        // Check meta if present
        if (resource.Meta?.VersionId != null)
        {
            _logger.LogDebug("Resource has version: {Version}", resource.Meta.VersionId);
        }

        // Type-specific validation
        switch (resource)
        {
            case Patient patient:
                ValidatePatient(patient, issues);
                break;
            case Observation observation:
                ValidateObservation(observation, issues);
                break;
            case Condition condition:
                ValidateCondition(condition, issues);
                break;
        }

        var result = issues.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(issues.ToArray());

        _logger.LogInformation("Local validation {Result}: {Count} issue(s)",
            result.IsValid ? "passed" : "failed", issues.Count);

        return result;
    }

    /// <summary>
    /// Validates a FHIR resource using the server's $validate operation.
    /// </summary>
    public async Task<ValidationResult> ValidateOnServerAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating {ResourceType} on FHIR server...", resource.TypeName);

        try
        {
            var parameters = new Parameters()
                .Add("resource", resource);

            var outcome = await _fhirService.TypeOperationAsync<OperationOutcome>(
                $"{resource.TypeName}/$validate", parameters);

            return ConvertOutcome(outcome);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex, "Server validation returned an error");

            if (ex.Outcome != null)
            {
                return ConvertOutcome(ex.Outcome);
            }

            return ValidationResult.Failure(new ValidationIssue
            {
                Severity = "error",
                Message = $"Server validation failed: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during server validation");
            return ValidationResult.Failure(new ValidationIssue
            {
                Severity = "error",
                Message = $"Validation error: {ex.Message}"
            });
        }
    }

    // ── Type-specific validations ──

    private void ValidatePatient(Patient patient, List<ValidationIssue> issues)
    {
        if (patient.Name == null || patient.Name.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "warning",
                Message = "Patient has no name",
                Location = "Patient.name"
            });
        }

        if (patient.Gender == null)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "warning",
                Message = "Patient gender is not specified",
                Location = "Patient.gender"
            });
        }
    }

    private void ValidateObservation(Observation observation, List<ValidationIssue> issues)
    {
        if (observation.Status == null)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Observation.status is required",
                Location = "Observation.status"
            });
        }

        if (observation.Code == null)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Observation.code is required",
                Location = "Observation.code"
            });
        }
    }

    private void ValidateCondition(Condition condition, List<ValidationIssue> issues)
    {
        if (condition.Code == null)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                Message = "Condition.code is required",
                Location = "Condition.code"
            });
        }
    }

    private ValidationResult ConvertOutcome(OperationOutcome outcome)
    {
        var issues = new List<ValidationIssue>();

        if (outcome.Issue != null)
        {
            foreach (var issue in outcome.Issue)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = issue.Severity?.ToString()?.ToLower() ?? "information",
                    Message = issue.Diagnostics ?? issue.Details?.Text ?? "No details",
                    Location = issue.Expression?.FirstOrDefault()
                });
            }
        }

        var hasErrors = issues.Any(i => i.Severity is "error" or "fatal");
        return new ValidationResult
        {
            IsValid = !hasErrors,
            Issues = issues
        };
    }
}
