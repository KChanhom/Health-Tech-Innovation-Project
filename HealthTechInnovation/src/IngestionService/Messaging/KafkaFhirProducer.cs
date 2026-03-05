using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace IngestionService.Messaging;

public interface IKafkaFhirProducer
{
    Task PublishAsync(IEnumerable<Resource> resources, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes FHIR resources as JSON messages to a Kafka topic.
/// Each message contains a single FHIR resource serialized as JSON.
/// </summary>
public class KafkaFhirProducer : IKafkaFhirProducer, IDisposable
{
    private const string DefaultTopic = "fhir.resources";

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaFhirProducer> _logger;
    private readonly FhirJsonSerializer _serializer = new();
    private bool _disposed;

    public KafkaFhirProducer(IProducer<string, string> producer, ILogger<KafkaFhirProducer> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishAsync(IEnumerable<Resource> resources, CancellationToken cancellationToken = default)
    {
        var resourceList = resources.ToList();
        if (resourceList.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Publishing {Count} FHIR resources to Kafka topic '{Topic}'",
            resourceList.Count, DefaultTopic);

        foreach (var resource in resourceList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = _serializer.SerializeToString(resource);
            var key = resource.TypeName ?? "Resource";

            try
            {
                var result = await _producer.ProduceAsync(
                    DefaultTopic,
                    new Message<string, string> { Key = key, Value = json },
                    cancellationToken);

                _logger.LogDebug(
                    "Published {ResourceType} to Kafka partition {Partition}, offset {Offset}",
                    resource.TypeName,
                    result.Partition.Value,
                    result.Offset.Value);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, "Failed to publish {ResourceType} to Kafka", resource.TypeName);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore flush errors on dispose
        }

        _producer.Dispose();
    }
}

