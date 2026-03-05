using Hl7.Fhir.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessingService.Messaging;
using ProcessingService.Persistence;
using ProcessingService.Terminology;
using ProcessingService.Validation;
using Task = System.Threading.Tasks.Task;

namespace ProcessingService;

/// <summary>
/// Background worker that simulates processing a message queue of FHIR resources.
/// Pipeline: Validate -> Enrich (Terminology) -> Persist
/// </summary>
public class ProcessingWorker : BackgroundService
{
    private readonly IKafkaFhirConsumer _consumer;
    private readonly FhirValidationService _validator;
    private readonly TerminologyService _terminologyService;
    private readonly ResourcePersistenceService _persistenceService;
    private readonly ILogger<ProcessingWorker> _logger;

    public ProcessingWorker(
        IKafkaFhirConsumer consumer,
        FhirValidationService validator,
        TerminologyService terminologyService,
        ResourcePersistenceService persistenceService,
        ILogger<ProcessingWorker> logger)
    {
        _consumer = consumer;
        _validator = validator;
        _terminologyService = terminologyService;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessingWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var resource = await _consumer.ConsumeAsync(stoppingToken);
            if (resource != null)
            {
                await ProcessResourceAsync(resource, stoppingToken);
            }
            else
            {
                await Task.Delay(1000, stoppingToken); // No message, short backoff
            }
        }
    }

    private async Task ProcessResourceAsync(Resource resource, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing {ResourceType}...", resource.TypeName);

        // 1. Validation
        var validationResult = _validator.ValidateLocally(resource);
        if (!validationResult.IsValid)
        {
            _logger.LogError("Validation failed for {ResourceType}: {Issues}", 
                resource.TypeName, string.Join(", ", validationResult.Issues));
            // In a real system: Move to Dead Letter Queue
            return;
        }

        // 2. Terminology Enrichment
        if (resource is Observation obs && obs.Code != null)
        {
            await _terminologyService.EnrichCodeableConceptAsync(obs.Code, cancellationToken);
        }
        else if (resource is Condition cond && cond.Code != null)
        {
            await _terminologyService.EnrichCodeableConceptAsync(cond.Code, cancellationToken);
        }

        // 3. Persistence
        var saveResult = await _persistenceService.SaveResourceAsync(resource, cancellationToken);
        if (saveResult.Success)
        {
            _logger.LogInformation("Successfully processed and saved {ResourceType} (ID: {Id})", 
                resource.TypeName, saveResult.ResourceId);
        }
        else
        {
            _logger.LogError("Failed to save {ResourceType}: {Error}", 
                resource.TypeName, saveResult.ErrorMessage);
        }
    }

}
