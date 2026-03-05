using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using IngestionService;
using IngestionService.Adapters;
using IngestionService.Messaging;

namespace HealthTechInnovation.Tests;

public class IngestionWorkerTests
{
    [Fact]
    public async Task RunIngestionCycleAsync_WithMultipleAdapters_PublishesAllResourcesToKafka()
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

        var publishedResources = new List<Resource>();
        var mockKafkaProducer = new Mock<IKafkaFhirProducer>();
        mockKafkaProducer
            .Setup(p => p.PublishAsync(It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Resource>, CancellationToken>((resources, _) =>
            {
                publishedResources.AddRange(resources);
            })
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            new[] { mockAdapter1.Object, mockAdapter2.Object },
            mockKafkaProducer.Object,
            logger.Object);

        // Act
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // Assert — each adapter contributed 1 resource → 2 total published
        mockKafkaProducer.Verify(p => p.PublishAsync(It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Equal(2, publishedResources.Count);
    }

    [Fact]
    public async Task RunIngestionCycleAsync_AdapterThrows_ContinuesWithOtherAdaptersAndPublishes()
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

        var publishedResources = new List<Resource>();
        var mockKafkaProducer = new Mock<IKafkaFhirProducer>();
        mockKafkaProducer
            .Setup(p => p.PublishAsync(It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Resource>, CancellationToken>((resources, _) =>
            {
                publishedResources.AddRange(resources);
            })
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            new[] { failingAdapter.Object, workingAdapter.Object },
            mockKafkaProducer.Object,
            logger.Object);

        // Act
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // Assert — the working adapter still got its resource published
        mockKafkaProducer.Verify(p => p.PublishAsync(It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(publishedResources);
    }

    [Fact]
    public async Task RunIngestionCycleAsync_NoAdapters_CompletesWithoutError()
    {
        // Arrange
        var mockKafkaProducer = new Mock<IKafkaFhirProducer>();
        var logger = new Mock<ILogger<IngestionWorker>>();
        var worker = new IngestionWorker(
            Enumerable.Empty<IDataSourceAdapter>(),
            mockKafkaProducer.Object,
            logger.Object);

        // Act & Assert — should not throw
        await worker.RunIngestionCycleAsync(CancellationToken.None);

        // No publishes should have been called
        mockKafkaProducer.Verify(p => p.PublishAsync(It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
