namespace Shared.Configuration;

/// <summary>
/// Configuration settings for FHIR server connectivity.
/// Maps to "FhirServer" section in appsettings.json.
/// </summary>
public class FhirServerSettings
{
    public const string SectionName = "FhirServer";

    /// <summary>
    /// Base URL of the FHIR server (e.g., "https://hapi.fhir.org/baseR4").
    /// </summary>
    public string BaseUrl { get; set; } = "https://hapi.fhir.org/baseR4";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Preferred resource format (json or xml).
    /// </summary>
    public string PreferredFormat { get; set; } = "json";

    /// <summary>
    /// Whether to verify server conformance on first request.
    /// </summary>
    public bool VerifyFhirVersion { get; set; } = false;
}
