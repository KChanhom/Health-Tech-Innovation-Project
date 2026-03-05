using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using IngestionService.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessingService.Messaging;

namespace HealthTechInnovation.Tests;

public class KafkaMessagingTests
{
    [Fact]
    public async Task KafkaFhirProducer_PublishAsync_SerializesAndProducesEachResource()
    {
        // Arrange
        var sentMessages = new List<Message<string, string>>();

        var mockProducer = new Mock<IProducer<string, string>>();
        mockProducer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, msg, _) =>
            {
                Assert.Equal("fhir.resources", topic);
                sentMessages.Add(msg);
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "fhir.resources",
                Partition = new Partition(0),
                Offset = new Offset(1)
            });

        var logger = new Mock<ILogger<KafkaFhirProducer>>();
        using var producer = new KafkaFhirProducer(mockProducer.Object, logger.Object);

        var resources = new Resource[]
        {
            new Patient { Name = new List<HumanName> { new() { Family = "Doe" } } },
            new Observation
            {
                Status = ObservationStatus.Final,
                Code = new CodeableConcept("http://loinc.org", "8867-4", "Heart rate")
            }
        };

        // Act
        await producer.PublishAsync(resources);

        // Assert
        Assert.Equal(2, sentMessages.Count);
        Assert.All(sentMessages, msg => Assert.False(string.IsNullOrWhiteSpace(msg.Value)));

        // Verify that messages are valid FHIR JSON
        var parser = new FhirJsonParser();
        foreach (var msg in sentMessages)
        {
            var resource = parser.Parse<Resource>(msg.Value);
            Assert.NotNull(resource);
        }
    }

    [Fact]
    public async Task KafkaFhirProducer_PublishAsync_EmptyCollection_DoesNotProduce()
    {
        // Arrange
        var mockProducer = new Mock<IProducer<string, string>>();
        var logger = new Mock<ILogger<KafkaFhirProducer>>();
        using var producer = new KafkaFhirProducer(mockProducer.Object, logger.Object);

        // Act
        await producer.PublishAsync(Array.Empty<Resource>());

        // Assert
        mockProducer.Verify(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task KafkaFhirConsumer_ConsumeAsync_WithValidMessage_ReturnsResource()
    {
        // Arrange
        var patient = new Patient
        {
            Name = new List<HumanName> { new() { Family = "Doe", Given = new[] { "Jane" } } }
        };
        var serializer = new FhirJsonSerializer();
        var json = serializer.SerializeToString(patient);

        var mockConsumer = new Mock<IConsumer<string, string>>();

        // Subscribe should be called once
        mockConsumer
            .Setup(c => c.Subscribe(It.IsAny<string>()))
            .Verifiable();

        mockConsumer
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(new ConsumeResult<string, string>
            {
                Message = new Message<string, string> { Key = "Patient", Value = json },
                TopicPartitionOffset = new TopicPartitionOffset("fhir.resources", new Partition(0), new Offset(1))
            });

        var logger = new Mock<ILogger<KafkaFhirConsumer>>();
        using var consumer = new KafkaFhirConsumer(mockConsumer.Object, logger.Object);

        // Act
        var result = await consumer.ConsumeAsync();

        // Assert
        mockConsumer.Verify(c => c.Subscribe("fhir.resources"), Times.Once);
        var resource = Assert.IsType<Patient>(result);
        Assert.Equal("Doe", resource.Name.First().Family);
    }

    [Fact]
    public async Task KafkaFhirConsumer_ConsumeAsync_OnConsumeException_ReturnsNull()
    {
        // Arrange
        var mockConsumer = new Mock<IConsumer<string, string>>();
        mockConsumer
            .Setup(c => c.Subscribe(It.IsAny<string>()))
            .Verifiable();

        mockConsumer
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Throws(new ConsumeException(new Error(ErrorCode.Local_AllBrokersDown), null));

        var logger = new Mock<ILogger<KafkaFhirConsumer>>();
        using var consumer = new KafkaFhirConsumer(mockConsumer.Object, logger.Object);

        // Act
        var result = await consumer.ConsumeAsync();

        // Assert
        Assert.Null(result);
    }
}

