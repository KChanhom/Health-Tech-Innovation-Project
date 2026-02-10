using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Configuration;

namespace Shared.Fhir;

/// <summary>
/// Factory that creates pre-configured FhirClient instances based on application settings.
/// Configures timeout, preferred format, and conformance checking.
/// </summary>
public class FhirClientFactory : IFhirClientFactory
{
    private readonly FhirServerSettings _settings;
    private readonly ILogger<FhirClientFactory> _logger;

    public FhirClientFactory(IOptions<FhirServerSettings> settings, ILogger<FhirClientFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new FhirClient with settings from configuration.
    /// </summary>
    public FhirClient CreateClient()
    {
        _logger.LogInformation("Creating FhirClient for server: {BaseUrl}", _settings.BaseUrl);

        var clientSettings = new FhirClientSettings
        {
            Timeout = _settings.TimeoutSeconds * 1000, // Convert seconds to milliseconds
            PreferredFormat = _settings.PreferredFormat.Equals("xml", StringComparison.OrdinalIgnoreCase)
                ? ResourceFormat.Xml
                : ResourceFormat.Json,
            VerifyFhirVersion = _settings.VerifyFhirVersion
        };

        var client = new FhirClient(_settings.BaseUrl, clientSettings);

        _logger.LogInformation(
            "FhirClient created — Timeout: {Timeout}s, Format: {Format}, VerifyVersion: {Verify}",
            _settings.TimeoutSeconds,
            _settings.PreferredFormat,
            _settings.VerifyFhirVersion);

        return client;
    }
}
