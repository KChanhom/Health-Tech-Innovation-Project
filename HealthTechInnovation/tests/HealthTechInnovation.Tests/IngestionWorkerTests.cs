using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using IngestionService;
using IngestionService.Adapters;
using Shared.Fhir;

namespace HealthTechInnovation.Tests;

public class IngestionWorkerTests
{
    [Fact]
    public async Task RunIngestionCycleAsync_WithMultipleAdapters_IngestsAllResources()
    {
        // Arrange
        var mockAdapter1 = new Mock<IDataSourceAdapter>();
        mockAdapter1.Setup(a => a.SourceName).Returns("TestSource1");
        mockAdapter1.Setup(a => a.FetchDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Patient { Name = new List<HumanName> { new() { Family = "Test1" } } }
            });

        var mockAdapter2 = new Mock<IDataSourceAdapter>();
        mockAdapter2.Setup(a => a.SourceName).Returns("TestSource2");
        mockAdapter2.Setup(a => a.FetchDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Observation
                {
                    Status = ObservationStatus.Final,
                    Code = new CodeableConcept("http://loinc.org", "1234-5", "Test")
                }
            });

        var mockCrudService = new Mock<IFhirCrudService>();
        mockCrudService.Setup(c => c.CreateResourceAsync(It.IsAny<Resource>()))
            .ReturnsAsync((Resource r) =>
            {
                r.Id = Guid.NewGuid().ToString();
                return r;
            });

        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            new[] { mockAdapter1.Object, mockAdapter2.Object },
            mockCrudService.Object,
            logger.Object);

        // Act
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // Assert — each adapter contributed 1 resource → 2 total creates
        mockCrudService.Verify(c => c.CreateResourceAsync(It.IsAny<Resource>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunIngestionCycleAsync_AdapterThrows_ContinuesWithOtherAdapters()
    {
        // Arrange
        var failingAdapter = new Mock<IDataSourceAdapter>();
        failingAdapter.Setup(a => a.SourceName).Returns("FailingSource");
        failingAdapter.Setup(a => a.FetchDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var workingAdapter = new Mock<IDataSourceAdapter>();
        workingAdapter.Setup(a => a.SourceName).Returns("WorkingSource");
        workingAdapter.Setup(a => a.FetchDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Resource>
            {
                new Patient { Name = new List<HumanName> { new() { Family = "Healthy" } } }
            });

        var mockCrudService = new Mock<IFhirCrudService>();
        mockCrudService.Setup(c => c.CreateResourceAsync(It.IsAny<Resource>()))
            .ReturnsAsync((Resource r) =>
            {
                r.Id = "created-1";
                return r;
            });

        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            new[] { failingAdapter.Object, workingAdapter.Object },
            mockCrudService.Object,
            logger.Object);

        // Act
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // Assert — the working adapter still got its resource created
        mockCrudService.Verify(c => c.CreateResourceAsync(It.IsAny<Resource>()), Times.Once);
    }

    [Fact]
    public async Task RunIngestionCycleAsync_NoAdapters_CompletesWithoutError()
    {
        // Arrange
        var mockCrudService = new Mock<IFhirCrudService>();
        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            Enumerable.Empty<IDataSourceAdapter>(),
            mockCrudService.Object,
            logger.Object);

        // Act & Assert — should not throw
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // No creates should have been called
        mockCrudService.Verify(c => c.CreateResourceAsync(It.IsAny<Resource>()), Times.Never);
    }
}
