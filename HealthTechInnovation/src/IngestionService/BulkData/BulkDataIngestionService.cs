using System.Net.Http.Headers;
using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace IngestionService.BulkData;

/// <summary>
/// Service for performing FHIR Bulk Data $export operations.
/// Supports initiating exports, polling for completion, and downloading/parsing NDJSON files.
/// </summary>
public class BulkDataIngestionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BulkDataIngestionService> _logger;
    private readonly FhirJsonDeserializer _deserializer;

    public BulkDataIngestionService(HttpClient httpClient, ILogger<BulkDataIngestionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _deserializer = new FhirJsonDeserializer();
    }

    /// <summary>
    /// Initiates a $export operation on the FHIR server.
    /// Returns the Content-Location URL for polling.
    /// </summary>
    /// <param name="fhirBaseUrl">Base URL of the FHIR server</param>
    /// <param name="resourceTypes">Optional comma-separated resource types to export (e.g. "Patient,Observation")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The polling URL (Content-Location header value)</returns>
    public async Task<string> StartExportAsync(
        string fhirBaseUrl,
        string? resourceTypes = null,
        CancellationToken cancellationToken = default)
    {
        var exportUrl = $"{fhirBaseUrl.TrimEnd('/')}/$export";
        if (!string.IsNullOrWhiteSpace(resourceTypes))
        {
            exportUrl += $"?_type={Uri.EscapeDataString(resourceTypes)}";
        }

        _logger.LogInformation("Starting Bulk Data export: {Url}", exportUrl);

        var request = new HttpRequestMessage(HttpMethod.Get, exportUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        request.Headers.Add("Prefer", "respond-async");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"$export request failed with status {response.StatusCode}: {body}");
        }

        var contentLocation = response.Headers.Location?.ToString()
            ?? response.Content.Headers.ContentLocation?.ToString()
            ?? throw new InvalidOperationException("No Content-Location header in $export response");

        _logger.LogInformation("Export initiated. Polling URL: {Url}", contentLocation);
        return contentLocation;
    }

    /// <summary>
    /// Polls the export status URL until the export is complete.
    /// Returns the list of output file URLs when ready.
    /// </summary>
    /// <param name="pollingUrl">The Content-Location URL from StartExportAsync</param>
    /// <param name="pollIntervalSeconds">Seconds between poll attempts</param>
    /// <param name="maxAttempts">Maximum number of poll attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of NDJSON file URLs</returns>
    public async Task<List<BulkExportFileInfo>> PollExportStatusAsync(
        string pollingUrl,
        int pollIntervalSeconds = 5,
        int maxAttempts = 120,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Polling export status at: {Url}", pollingUrl);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _httpClient.GetAsync(pollingUrl, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var files = ParseExportOutput(json);
                _logger.LogInformation("Export complete. {Count} file(s) available", files.Count);
                return files;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                var progress = response.Headers.Contains("X-Progress")
                    ? response.Headers.GetValues("X-Progress").FirstOrDefault()
                    : "unknown";

                _logger.LogInformation(
                    "Export in progress (attempt {Attempt}/{Max}). Progress: {Progress}",
                    attempt, maxAttempts, progress);

                await System.Threading.Tasks.Task.Delay(
                    TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken);
                continue;
            }

            throw new InvalidOperationException(
                $"Unexpected status {response.StatusCode} while polling export");
        }

        throw new TimeoutException($"Export did not complete within {maxAttempts} attempts");
    }

    /// <summary>
    /// Downloads and parses NDJSON files into FHIR resources.
    /// </summary>
    /// <param name="fileUrls">List of NDJSON file URLs from the export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of parsed FHIR resources</returns>
    public async Task<List<Resource>> DownloadAndParseNdjsonAsync(
        List<BulkExportFileInfo> fileUrls,
        CancellationToken cancellationToken = default)
    {
        var allResources = new List<Resource>();

        foreach (var fileInfo in fileUrls)
        {
            _logger.LogInformation("Downloading NDJSON file: {Type} from {Url}",
                fileInfo.ResourceType, fileInfo.Url);

            var ndjsonContent = await _httpClient.GetStringAsync(fileInfo.Url, cancellationToken);
            var resources = ParseNdjsonResources(ndjsonContent);

            _logger.LogInformation("Parsed {Count} {Type} resources from file",
                resources.Count, fileInfo.ResourceType);

            allResources.AddRange(resources);
        }

        _logger.LogInformation("Total resources downloaded: {Count}", allResources.Count);
        return allResources;
    }

    /// <summary>
    /// Parses NDJSON content (one JSON resource per line) into FHIR Resource objects.
    /// </summary>
    public List<Resource> ParseNdjsonResources(string ndjsonContent)
    {
        var resources = new List<Resource>();
        var lines = ndjsonContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                var resource = _deserializer.DeserializeResource(line.Trim());
                resources.Add(resource);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse NDJSON line: {Line}",
                    line.Length > 100 ? line[..100] + "..." : line);
            }
        }

        return resources;
    }

    // ── Helpers ──

    private List<BulkExportFileInfo> ParseExportOutput(string json)
    {
        // Parse the $export completion response to extract file URLs
        // The response has format: { "output": [{ "type": "...", "url": "..." }, ...] }
        var files = new List<BulkExportFileInfo>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("output", out var outputArray))
            {
                foreach (var item in outputArray.EnumerateArray())
                {
                    var resourceType = item.GetProperty("type").GetString() ?? "Unknown";
                    var url = item.GetProperty("url").GetString() ?? "";
                    files.Add(new BulkExportFileInfo(resourceType, url));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse export output JSON");
        }

        return files;
    }
}

/// <summary>
/// Represents a file from the Bulk Data $export output.
/// </summary>
public record BulkExportFileInfo(string ResourceType, string Url);
