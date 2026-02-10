using Hl7.Fhir.Rest;

namespace Shared.Fhir;

/// <summary>
/// Factory interface for creating configured FhirClient instances.
/// </summary>
public interface IFhirClientFactory
{
    /// <summary>
    /// Creates a new FhirClient configured with the server settings.
    /// </summary>
    FhirClient CreateClient();
}
