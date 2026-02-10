using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Shared.Fhir;
using Task = System.Threading.Tasks.Task;

namespace ProcessingService.Terminology;

/// <summary>
/// Service for interacting with FHIR terminology services.
/// Validates codes, performs lookups, and translates between code systems
/// (ICD-10, SNOMED-CT, LOINC, etc.).
/// </summary>
public class TerminologyService
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<TerminologyService> _logger;

    // Well-known code system URIs
    public static class CodeSystems
    {
        public const string SnomedCt = "http://snomed.info/sct";
        public const string Loinc = "http://loinc.org";
        public const string Icd10 = "http://hl7.org/fhir/sid/icd-10";
        public const string Icd10CM = "http://hl7.org/fhir/sid/icd-10-cm";
        public const string RxNorm = "http://www.nlm.nih.gov/research/umls/rxnorm";
    }

    public TerminologyService(IFhirCrudService fhirService, ILogger<TerminologyService> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    /// <summary>
    /// Validates whether a code is valid within a given code system using $validate-code.
    /// </summary>
    public async Task<CodeValidationResult> ValidateCodeAsync(
        string system,
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating code {Code} in system {System}", code, system);

        try
        {
            var parameters = new Parameters()
                .Add("system", new FhirUri(system))
                .Add("code", new Code(code));

            var result = await _fhirService.TypeOperationAsync<Parameters>(
                "CodeSystem/$validate-code", parameters);

            var isValid = result?.Parameter
                ?.FirstOrDefault(p => p.Name == "result")?.Value is FhirBoolean b && b.Value == true;

            var display = result?.Parameter
                ?.FirstOrDefault(p => p.Name == "display")?.Value?.ToString();

            _logger.LogInformation("Code {Code} validation: {Result}", code, isValid ? "valid" : "invalid");

            return new CodeValidationResult
            {
                IsValid = isValid,
                Code = code,
                System = system,
                Display = display
            };
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex, "Code validation failed for {Code} in {System}", code, system);
            return new CodeValidationResult
            {
                IsValid = false,
                Code = code,
                System = system,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Looks up the display name and properties of a code using $lookup.
    /// </summary>
    public async Task<CodeLookupResult> LookupCodeAsync(
        string system,
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Looking up code {Code} in system {System}", code, system);

        try
        {
            var parameters = new Parameters()
                .Add("system", new FhirUri(system))
                .Add("code", new Code(code));

            var result = await _fhirService.TypeOperationAsync<Parameters>(
                "CodeSystem/$lookup", parameters);

            var display = result?.Parameter
                ?.FirstOrDefault(p => p.Name == "display")?.Value?.ToString();

            var name = result?.Parameter
                ?.FirstOrDefault(p => p.Name == "name")?.Value?.ToString();

            _logger.LogInformation("Code {Code} lookup: display='{Display}'", code, display);

            return new CodeLookupResult
            {
                Found = true,
                Code = code,
                System = system,
                Display = display ?? "",
                CodeSystemName = name ?? ""
            };
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex, "Code lookup failed for {Code} in {System}", code, system);
            return new CodeLookupResult
            {
                Found = false,
                Code = code,
                System = system,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Enriches a CodeableConcept by adding display names from terminology services.
    /// Falls back gracefully if the server doesn't support $lookup.
    /// </summary>
    public async Task<CodeableConcept> EnrichCodeableConceptAsync(
        CodeableConcept concept,
        CancellationToken cancellationToken = default)
    {
        if (concept.Coding == null || concept.Coding.Count == 0)
            return concept;

        foreach (var coding in concept.Coding)
        {
            if (!string.IsNullOrEmpty(coding.System) && !string.IsNullOrEmpty(coding.Code)
                && string.IsNullOrEmpty(coding.Display))
            {
                try
                {
                    var lookup = await LookupCodeAsync(coding.System, coding.Code, cancellationToken);
                    if (lookup.Found && !string.IsNullOrEmpty(lookup.Display))
                    {
                        coding.Display = lookup.Display;
                        _logger.LogDebug("Enriched {Code} with display '{Display}'",
                            coding.Code, coding.Display);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not enrich code {Code}, skipping", coding.Code);
                }
            }
        }

        return concept;
    }
}

/// <summary>
/// Result of a code validation operation.
/// </summary>
public class CodeValidationResult
{
    public bool IsValid { get; set; }
    public string Code { get; set; } = "";
    public string System { get; set; } = "";
    public string? Display { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a code lookup operation.
/// </summary>
public class CodeLookupResult
{
    public bool Found { get; set; }
    public string Code { get; set; } = "";
    public string System { get; set; } = "";
    public string Display { get; set; } = "";
    public string CodeSystemName { get; set; } = "";
    public string? ErrorMessage { get; set; }
}
