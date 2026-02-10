using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace IngestionService.Adapters;

/// <summary>
/// Adapter for IoT medical devices (e.g., heart rate monitors, blood pressure cuffs).
/// Fetches Observation resources representing vital signs.
/// </summary>
public class IoTAdapter : IDataSourceAdapter
{
    private readonly ILogger<IoTAdapter> _logger;

    public string SourceName => "IoT Medical Devices";

    public IoTAdapter(ILogger<IoTAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<Resource>> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Source}] Fetching data from IoT devices...", SourceName);

        // Simulate fetching data from IoT medical devices
        await Task.Delay(50, cancellationToken);

        var resources = new List<Resource>();

        // ── Heart Rate Observation ──
        var heartRate = new Observation
        {
            Status = ObservationStatus.Final,
            Category = new List<CodeableConcept>
            {
                new("http://terminology.hl7.org/CodeSystem/observation-category", "vital-signs", "Vital Signs")
            },
            Code = new CodeableConcept("http://loinc.org", "8867-4", "Heart rate"),
            Value = new Quantity(72, "/min", "http://unitsofmeasure.org"),
            Effective = new FhirDateTime(DateTimeOffset.UtcNow.ToString("o")),
            Device = new ResourceReference { Display = "SmartWatch Model X" }
        };
        resources.Add(heartRate);

        // ── Blood Pressure Observation ──
        var bloodPressure = new Observation
        {
            Status = ObservationStatus.Final,
            Category = new List<CodeableConcept>
            {
                new("http://terminology.hl7.org/CodeSystem/observation-category", "vital-signs", "Vital Signs")
            },
            Code = new CodeableConcept("http://loinc.org", "85354-9", "Blood pressure panel"),
            Component = new List<Observation.ComponentComponent>
            {
                new()
                {
                    Code = new CodeableConcept("http://loinc.org", "8480-6", "Systolic blood pressure"),
                    Value = new Quantity(120, "mmHg", "http://unitsofmeasure.org")
                },
                new()
                {
                    Code = new CodeableConcept("http://loinc.org", "8462-4", "Diastolic blood pressure"),
                    Value = new Quantity(80, "mmHg", "http://unitsofmeasure.org")
                }
            },
            Effective = new FhirDateTime(DateTimeOffset.UtcNow.ToString("o")),
            Device = new ResourceReference { Display = "BP Monitor Pro" }
        };
        resources.Add(bloodPressure);

        // ── SpO2 Observation ──
        var spo2 = new Observation
        {
            Status = ObservationStatus.Final,
            Category = new List<CodeableConcept>
            {
                new("http://terminology.hl7.org/CodeSystem/observation-category", "vital-signs", "Vital Signs")
            },
            Code = new CodeableConcept("http://loinc.org", "2708-6", "Oxygen saturation in Arterial blood"),
            Value = new Quantity(98, "%", "http://unitsofmeasure.org"),
            Effective = new FhirDateTime(DateTimeOffset.UtcNow.ToString("o")),
            Device = new ResourceReference { Display = "Pulse Oximeter Z" }
        };
        resources.Add(spo2);

        _logger.LogInformation("[{Source}] Fetched {Count} vital sign observations", SourceName, resources.Count);
        return resources;
    }
}
