using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace ProcessingService.Messaging;

public interface IKafkaFhirConsumer
{
    Task<Resource?> ConsumeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Consumes FHIR resources from a Kafka topic and deserializes them from JSON.
/// </summary>
public class KafkaFhirConsumer : IKafkaFhirConsumer, IDisposable
{
    private const string Topic = "fhir.resources";

    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaFhirConsumer> _logger;
    private readonly FhirJsonParser _parser = new();
    private bool _subscribed;
    private bool _disposed;

    public KafkaFhirConsumer(IConsumer<string, string> consumer, ILogger<KafkaFhirConsumer> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public Task<Resource?> ConsumeAsync(CancellationToken cancellationToken = default)
    {
        if (!_subscribed)
        {
            _consumer.Subscribe(Topic);
            _subscribed = true;
            _logger.LogInformation("Subscribed to Kafka topic '{Topic}'", Topic);
        }

        try
        {
            // Poll with timeout so we can honor cancellation.
            var result = _consumer.Consume(cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.Message?.Value))
            {
                return Task.FromResult<Resource?>(null);
            }

            var json = result.Message.Value;
            var resource = _parser.Parse<Resource>(json);

            _logger.LogDebug(
                "Consumed {ResourceType} from Kafka partition {Partition}, offset {Offset}",
                resource.TypeName,
                result.Partition.Value,
                result.Offset.Value);

            return Task.FromResult<Resource?>(resource);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Kafka consume error");
            return Task.FromResult<Resource?>(null);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_subscribed)
            {
                _consumer.Close();
            }
        }
        catch
        {
            // ignore errors on close
        }

        _consumer.Dispose();
    }
}

