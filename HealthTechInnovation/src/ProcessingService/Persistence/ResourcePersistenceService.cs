using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Shared.Fhir;
using Task = System.Threading.Tasks.Task;

namespace ProcessingService.Persistence;

/// <summary>
/// Service for persisting FHIR resources to the server.
/// Supports single create/update and batch Transaction bundles.
/// Handles FHIR server responses (201 Created, 200 OK, errors).
/// </summary>
public class ResourcePersistenceService
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<ResourcePersistenceService> _logger;

    public ResourcePersistenceService(IFhirCrudService fhirService, ILogger<ResourcePersistenceService> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    /// <summary>
    /// Saves a single resource. Creates if no ID, updates if ID exists.
    /// </summary>
    public async Task<PersistenceResult> SaveResourceAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving {ResourceType} (ID: {Id})...",
            resource.TypeName, resource.Id ?? "new");

        try
        {
            Resource saved;

            if (string.IsNullOrEmpty(resource.Id))
            {
                // Create
                saved = await _fhirService.CreateResourceAsync(resource);
                _logger.LogInformation("{ResourceType} created with ID: {Id}", saved.TypeName, saved.Id);

                return new PersistenceResult
                {
                    Success = true,
                    ResourceId = saved.Id ?? "",
                    Operation = "Create",
                    StatusCode = 201
                };
            }
            else
            {
                // Update
                saved = await _fhirService.UpdateResourceAsync(resource);
                _logger.LogInformation("{ResourceType} updated, ID: {Id}", saved.TypeName, saved.Id);

                return new PersistenceResult
                {
                    Success = true,
                    ResourceId = saved.Id ?? "",
                    Operation = "Update",
                    StatusCode = 200
                };
            }
        }
        catch (FhirOperationException ex)
        {
            _logger.LogError(ex, "Failed to save {ResourceType}", resource.TypeName);
            return new PersistenceResult
            {
                Success = false,
                Operation = string.IsNullOrEmpty(resource.Id) ? "Create" : "Update",
                StatusCode = (int)ex.Status,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Saves multiple resources in a single FHIR Transaction bundle.
    /// All entries succeed or all fail (atomic).
    /// </summary>
    public async Task<BatchPersistenceResult> SaveBatchAsync(
        IEnumerable<Resource> resources,
        CancellationToken cancellationToken = default)
    {
        var resourceList = resources.ToList();
        _logger.LogInformation("Saving batch of {Count} resources via Transaction...", resourceList.Count);

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = new List<Bundle.EntryComponent>()
        };

        foreach (var resource in resourceList)
        {
            var entry = new Bundle.EntryComponent
            {
                Resource = resource,
                Request = new Bundle.RequestComponent
                {
                    Method = string.IsNullOrEmpty(resource.Id)
                        ? Bundle.HTTPVerb.POST
                        : Bundle.HTTPVerb.PUT,
                    Url = string.IsNullOrEmpty(resource.Id)
                        ? resource.TypeName
                        : $"{resource.TypeName}/{resource.Id}"
                }
            };
            bundle.Entry.Add(entry);
        }

        try
        {
            var response = await _fhirService.TransactionAsync(bundle);

            var results = new List<PersistenceResult>();
            if (response.Entry != null)
            {
                foreach (var entry in response.Entry)
                {
                    var statusCode = ParseStatusCode(entry.Response?.Status);
                    results.Add(new PersistenceResult
                    {
                        Success = statusCode >= 200 && statusCode < 300,
                        ResourceId = entry.Resource?.Id ?? "",
                        Operation = statusCode == 201 ? "Create" : "Update",
                        StatusCode = statusCode
                    });
                }
            }

            _logger.LogInformation("Transaction completed. {Success}/{Total} succeeded",
                results.Count(r => r.Success), results.Count);

            return new BatchPersistenceResult
            {
                Success = results.All(r => r.Success),
                TotalCount = resourceList.Count,
                SuccessCount = results.Count(r => r.Success),
                Results = results
            };
        }
        catch (FhirOperationException ex)
        {
            _logger.LogError(ex, "Transaction failed for batch of {Count} resources", resourceList.Count);
            return new BatchPersistenceResult
            {
                Success = false,
                TotalCount = resourceList.Count,
                SuccessCount = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    private int ParseStatusCode(string? status)
    {
        if (string.IsNullOrEmpty(status)) return 0;
        // Status format: "200 OK", "201 Created", etc.
        var parts = status.Split(' ');
        return int.TryParse(parts[0], out var code) ? code : 0;
    }
}

/// <summary>
/// Result of persisting a single FHIR resource.
/// </summary>
public class PersistenceResult
{
    public bool Success { get; set; }
    public string? ResourceId { get; set; }
    public string Operation { get; set; } = "";
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of persisting a batch of FHIR resources.
/// </summary>
public class BatchPersistenceResult
{
    public bool Success { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<PersistenceResult> Results { get; set; } = new();
}
