using System.Net;
using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using IngestionService.BulkData;

namespace HealthTechInnovation.Tests;

public class BulkDataIngestionServiceTests
{
    [Fact]
    public void ParseNdjsonResources_ValidNdjson_ReturnsResources()
    {
        // Arrange
        var serializer = new FhirJsonSerializer();
        var patient = new Patient
        {
            Id = "test-1",
            Name = new List<HumanName> { new() { Family = "Test" } }
        };
        var observation = new Observation
        {
            Id = "obs-1",
            Status = ObservationStatus.Final,
            Code = new CodeableConcept("http://loinc.org", "12345-6", "Test Code")
        };

        var ndjson = serializer.SerializeToString(patient)
            + "\n" + serializer.SerializeToString(observation);

        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var logger = new Mock<ILogger<BulkDataIngestionService>>();
        var service = new BulkDataIngestionService(httpClient, logger.Object);

        // Act
        var resources = service.ParseNdjsonResources(ndjson);

        // Assert
        Assert.Equal(2, resources.Count);
        Assert.IsType<Patient>(resources[0]);
        Assert.IsType<Observation>(resources[1]);
    }

    [Fact]
    public void ParseNdjsonResources_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var logger = new Mock<ILogger<BulkDataIngestionService>>();
        var service = new BulkDataIngestionService(httpClient, logger.Object);

        // Act
        var resources = service.ParseNdjsonResources("");

        // Assert
        Assert.Empty(resources);
    }

    [Fact]
    public void ParseNdjsonResources_InvalidJson_SkipsInvalidLines()
    {
        // Arrange
        var serializer = new FhirJsonSerializer();
        var patient = new Patient
        {
            Id = "valid-1",
            Name = new List<HumanName> { new() { Family = "Valid" } }
        };

        var ndjson = serializer.SerializeToString(patient) + "\n{invalid json}\n";

        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var logger = new Mock<ILogger<BulkDataIngestionService>>();
        var service = new BulkDataIngestionService(httpClient, logger.Object);

        // Act
        var resources = service.ParseNdjsonResources(ndjson);

        // Assert
        Assert.Single(resources);
        Assert.IsType<Patient>(resources[0]);
    }

    [Fact]
    public async Task StartExportAsync_Returns202_ReturnsPollingUrl()
    {
        // Arrange
        var pollingUrl = "https://fhir.example.com/export-status/123";
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Headers = { Location = new Uri(pollingUrl) }
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var logger = new Mock<ILogger<BulkDataIngestionService>>();
        var service = new BulkDataIngestionService(httpClient, logger.Object);

        // Act
        var result = await service.StartExportAsync("https://fhir.example.com/r4");

        // Assert
        Assert.Equal(pollingUrl, result);
    }

    [Fact]
    public async Task StartExportAsync_NonAcceptedStatus_ThrowsException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Bad request")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var logger = new Mock<ILogger<BulkDataIngestionService>>();
        var service = new BulkDataIngestionService(httpClient, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartExportAsync("https://fhir.example.com/r4"));
    }
}
