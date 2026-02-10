using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Shared.Fhir;

/// <summary>
/// Service interface for basic FHIR CRUD operations.
/// </summary>
public interface IFhirCrudService
{
    /// <summary>
    /// Creates a new Patient resource on the FHIR server.
    /// </summary>
    Task<Patient> CreatePatientAsync(Patient patient);

    /// <summary>
    /// Reads a Patient resource by its ID.
    /// </summary>
    Task<Patient?> ReadPatientAsync(string id);

    /// <summary>
    /// Searches for Patient resources using optional search criteria.
    /// </summary>
    Task<Bundle> SearchPatientsAsync(string[]? searchCriteria = null);

    /// <summary>
    /// Updates an existing Patient resource.
    /// </summary>
    Task<Patient> UpdatePatientAsync(Patient patient);

    /// <summary>
    /// Deletes a Patient resource by its ID.
    /// </summary>
    Task DeletePatientAsync(string id);

    /// <summary>
    /// Creates a generic FHIR resource.
    /// </summary>
    Task<T> CreateResourceAsync<T>(T resource) where T : Resource;

    /// <summary>
    /// Reads a generic FHIR resource by type and ID.
    /// </summary>
    Task<T?> ReadResourceAsync<T>(string id) where T : Resource, new();

    /// <summary>
    /// Searches for FHIR resources of a given type.
    /// </summary>
    Task<Bundle> SearchResourcesAsync<T>(string[]? searchCriteria = null) where T : Resource, new();

    /// <summary>
    /// Updates a generic FHIR resource.
    /// </summary>
    Task<T> UpdateResourceAsync<T>(T resource) where T : Resource;

    /// <summary>
    /// Deletes a generic FHIR resource by type and ID.
    /// </summary>
    Task DeleteResourceAsync(string resourceType, string id);

    /// <summary>
    /// Executes a FHIR transaction bundle.
    /// </summary>
    Task<Bundle> TransactionAsync(Bundle bundle);

    /// <summary>
    /// Executes a FHIR type operation (e.g. $validate, $lookup).
    /// </summary>
    Task<TResource> TypeOperationAsync<TResource>(string location, Parameters parameters) where TResource : Resource;
}
