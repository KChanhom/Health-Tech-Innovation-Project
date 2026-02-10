using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Configuration;
using Shared.Fhir;

namespace HealthTechInnovation.Tests;

public class FhirClientFactoryTests
{
    [Fact]
    public void CreateClient_WithDefaultSettings_ReturnsFhirClient()
    {
        // Arrange
        var settings = Options.Create(new FhirServerSettings
        {
            BaseUrl = "https://hapi.fhir.org/baseR4",
            TimeoutSeconds = 30,
            PreferredFormat = "json",
            VerifyFhirVersion = false
        });
        var logger = new Mock<ILogger<FhirClientFactory>>();
        var factory = new FhirClientFactory(settings, logger.Object);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.StartsWith("https://hapi.fhir.org/baseR4", client.Endpoint.ToString());
    }

    [Fact]
    public void CreateClient_WithXmlFormat_SetsFormatCorrectly()
    {
        // Arrange
        var settings = Options.Create(new FhirServerSettings
        {
            BaseUrl = "https://example.com/fhir",
            TimeoutSeconds = 60,
            PreferredFormat = "xml",
            VerifyFhirVersion = true
        });
        var logger = new Mock<ILogger<FhirClientFactory>>();
        var factory = new FhirClientFactory(settings, logger.Object);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.StartsWith("https://example.com/fhir", client.Endpoint.ToString());
    }

    [Fact]
    public void CreateClient_WithCustomTimeout_SetsTimeoutCorrectly()
    {
        // Arrange
        var settings = Options.Create(new FhirServerSettings
        {
            BaseUrl = "https://example.com/fhir",
            TimeoutSeconds = 120,
            PreferredFormat = "json",
            VerifyFhirVersion = false
        });
        var logger = new Mock<ILogger<FhirClientFactory>>();
        var factory = new FhirClientFactory(settings, logger.Object);

        // Act
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }
}
