using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace Shared.Fhir;

/// <summary>
/// Provides basic CRUD operations against a FHIR server.
/// Uses FhirClient from the Firely SDK for all interactions.
/// </summary>
public class FhirCrudService : IFhirCrudService
{
    private readonly FhirClient _client;
    private readonly ILogger<FhirCrudService> _logger;

    public FhirCrudService(IFhirClientFactory clientFactory, ILogger<FhirCrudService> logger)
    {
        _client = clientFactory.CreateClient();
        _logger = logger;
    }

    // ───────────────────────── Patient-specific ─────────────────────────

    public async Task<Patient> CreatePatientAsync(Patient patient)
    {
        _logger.LogInformation("Creating Patient resource...");

        var created = await _client.CreateAsync(patient);

        _logger.LogInformation("Patient created with ID: {Id}", created.Id);
        return created;
    }

    public async Task<Patient?> ReadPatientAsync(string id)
    {
        _logger.LogInformation("Reading Patient with ID: {Id}", id);

        try
        {
            var patient = await _client.ReadAsync<Patient>($"Patient/{id}");
            return patient;
        }
        catch (FhirOperationException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Patient with ID {Id} not found", id);
            return null;
        }
    }

    public async Task<Bundle> SearchPatientsAsync(string[]? searchCriteria = null)
    {
        _logger.LogInformation("Searching for Patients with criteria: {Criteria}",
            searchCriteria != null ? string.Join(", ", searchCriteria) : "none");

        var bundle = await _client.SearchAsync<Patient>(searchCriteria);

        _logger.LogInformation("Search returned {Count} results",
            bundle.Entry?.Count ?? 0);

        return bundle;
    }

    public async Task<Patient> UpdatePatientAsync(Patient patient)
    {
        _logger.LogInformation("Updating Patient with ID: {Id}", patient.Id);

        var updated = await _client.UpdateAsync(patient);

        _logger.LogInformation("Patient updated successfully");
        return updated;
    }

    public async Task DeletePatientAsync(string id)
    {
        _logger.LogInformation("Deleting Patient with ID: {Id}", id);

        await _client.DeleteAsync($"Patient/{id}");

        _logger.LogInformation("Patient deleted successfully");
    }

    // ───────────────────────── Generic resources ─────────────────────────

    public async Task<T> CreateResourceAsync<T>(T resource) where T : Resource
    {
        _logger.LogInformation("Creating {ResourceType} resource...", typeof(T).Name);

        var created = await _client.CreateAsync(resource);

        _logger.LogInformation("{ResourceType} created with ID: {Id}",
            typeof(T).Name, created.Id);
        return created;
    }

    public async Task<T?> ReadResourceAsync<T>(string id) where T : Resource, new()
    {
        _logger.LogInformation("Reading {ResourceType} with ID: {Id}", typeof(T).Name, id);

        try
        {
            var resourceType = ModelInfo.GetFhirTypeNameForType(typeof(T));
            var resource = await _client.ReadAsync<T>($"{resourceType}/{id}");
            return resource;
        }
        catch (FhirOperationException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("{ResourceType} with ID {Id} not found", typeof(T).Name, id);
            return null;
        }
    }

    public async Task<Bundle> SearchResourcesAsync<T>(string[]? searchCriteria = null) where T : Resource, new()
    {
        _logger.LogInformation("Searching for {ResourceType} resources...", typeof(T).Name);

        var bundle = await _client.SearchAsync<T>(searchCriteria);

        _logger.LogInformation("Search returned {Count} {ResourceType} results",
            bundle.Entry?.Count ?? 0, typeof(T).Name);

        return bundle;
    }

    public async Task<T> UpdateResourceAsync<T>(T resource) where T : Resource
    {
        _logger.LogInformation("Updating {ResourceType} resource ID: {Id}", typeof(T).Name, resource.Id);
        return await _client.UpdateAsync(resource);
    }

    public async Task DeleteResourceAsync(string resourceType, string id)
    {
        _logger.LogInformation("Deleting {ResourceType} resource ID: {Id}", resourceType, id);
        await _client.DeleteAsync($"{resourceType}/{id}");
    }

    public async Task<Bundle> TransactionAsync(Bundle bundle)
    {
        _logger.LogInformation("Executing Transaction bundle with {Count} entries", bundle.Entry?.Count ?? 0);
        return await _client.TransactionAsync(bundle);
    }

    public async Task<TResource> TypeOperationAsync<TResource>(string location, Parameters parameters) where TResource : Resource
    {
        // _logger.LogInformation("Executing type operation at {Location}", location);
        // Note: Using the overload that takes string location and parameters.
        return (TResource)await _client.TypeOperationAsync<TResource>(location, parameters);
    }
}
