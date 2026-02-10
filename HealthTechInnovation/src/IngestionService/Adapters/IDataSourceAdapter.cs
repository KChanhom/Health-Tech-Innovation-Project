using Hl7.Fhir.Model;

namespace IngestionService.Adapters;

/// <summary>
/// Interface for data source adapters that fetch data from external systems
/// and convert them to FHIR resources.
/// </summary>
public interface IDataSourceAdapter
{
    /// <summary>
    /// Human-readable name of the data source.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Fetches data from the data source and converts them to FHIR resources.
    /// </summary>
    Task<IEnumerable<Resource>> FetchDataAsync(CancellationToken cancellationToken = default);
}
