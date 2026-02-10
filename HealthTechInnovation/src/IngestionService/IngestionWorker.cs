using IngestionService.Adapters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Fhir;

namespace IngestionService;

/// <summary>
/// Background worker that orchestrates data ingestion from multiple data source adapters.
/// Runs on a configurable schedule to pull data and push it to the FHIR server.
/// </summary>
public class IngestionWorker : BackgroundService
{
    private readonly IEnumerable<IDataSourceAdapter> _adapters;
    private readonly IFhirCrudService _fhirCrudService;
    private readonly ILogger<IngestionWorker> _logger;
    private readonly TimeSpan _pollingInterval;

    public IngestionWorker(
        IEnumerable<IDataSourceAdapter> adapters,
        IFhirCrudService fhirCrudService,
        ILogger<IngestionWorker> logger)
    {
        _adapters = adapters;
        _fhirCrudService = fhirCrudService;
        _logger = logger;
        _pollingInterval = TimeSpan.FromMinutes(5); // Configurable via settings
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionWorker starting. Registered adapters: {Adapters}",
            string.Join(", ", _adapters.Select(a => a.SourceName)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIngestionCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("IngestionWorker shutting down gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ingestion cycle. Will retry after interval.");
            }

            await System.Threading.Tasks.Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Runs one full ingestion cycle across all registered adapters.
    /// </summary>
    public async System.Threading.Tasks.Task RunIngestionCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("──────── Starting ingestion cycle ────────");

        int totalIngested = 0;
        int totalErrors = 0;

        foreach (var adapter in _adapters)
        {
            try
            {
                _logger.LogInformation("Processing adapter: {Source}", adapter.SourceName);

                var resources = await adapter.FetchDataAsync(cancellationToken);
                var resourceList = resources.ToList();

                _logger.LogInformation("Fetched {Count} resources from {Source}",
                    resourceList.Count, adapter.SourceName);

                foreach (var resource in resourceList)
                {
                    try
                    {
                        var created = await _fhirCrudService.CreateResourceAsync(resource);
                        totalIngested++;

                        _logger.LogDebug("Ingested {Type} with ID {Id} from {Source}",
                            resource.TypeName, created.Id, adapter.SourceName);
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        _logger.LogWarning(ex, "Failed to ingest {Type} from {Source}",
                            resource.TypeName, adapter.SourceName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Adapter {Source} failed entirely", adapter.SourceName);
            }
        }

        _logger.LogInformation(
            "──────── Ingestion cycle complete ──────── Ingested: {Ingested}, Errors: {Errors}",
            totalIngested, totalErrors);
    }
}
