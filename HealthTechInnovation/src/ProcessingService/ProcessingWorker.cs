using Hl7.Fhir.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly FhirValidationService _validator;
    private readonly TerminologyService _terminologyService;
    private readonly ResourcePersistenceService _persistenceService;
    private readonly ILogger<ProcessingWorker> _logger;
    
    // In a real system, this would come from a message bus (RabbitMQ, Azure Service Bus, etc.)
    private readonly Queue<Resource> _simulationQueue = new();

    public ProcessingWorker(
        FhirValidationService validator,
        TerminologyService terminologyService,
        ResourcePersistenceService persistenceService,
        ILogger<ProcessingWorker> logger)
    {
        _validator = validator;
        _terminologyService = terminologyService;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessingWorker starting...");

        // Simulate receiving some messages
        EnqueueSimulationData();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_simulationQueue.TryDequeue(out var resource))
            {
                await ProcessResourceAsync(resource, stoppingToken);
            }
            else
            {
                await Task.Delay(5000, stoppingToken); // Wait for more messages
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

    private void EnqueueSimulationData()
    {
        _logger.LogInformation("Enqueuing simulation data...");

        var patient = new Patient
        {
            Name = new List<HumanName> { new() { Family = "Doe", Given = new[] { "Jane" } } },
            Gender = AdministrativeGender.Female,
            BirthDate = "1990-05-20"
        };
        _simulationQueue.Enqueue(patient);

        var observation = new Observation
        {
            Status = ObservationStatus.Final,
            Code = new CodeableConcept("http://loinc.org", "8867-4", null), // Missing display, should be enriched
            Value = new Quantity(80, "/min", "http://unitsofmeasure.org"),
            Effective = new FhirDateTime(DateTimeOffset.UtcNow.ToString("o"))
        };
        _simulationQueue.Enqueue(observation);
    }
}
