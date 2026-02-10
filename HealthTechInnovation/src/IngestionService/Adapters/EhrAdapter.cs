using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace IngestionService.Adapters;

/// <summary>
/// Adapter for Electronic Health Record (EHR) systems.
/// Fetches Patient, Condition, and Observation resources from the EHR.
/// </summary>
public class EhrAdapter : IDataSourceAdapter
{
    private readonly ILogger<EhrAdapter> _logger;

    public string SourceName => "EHR System";

    public EhrAdapter(ILogger<EhrAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<Resource>> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Source}] Fetching data from EHR system...", SourceName);

        // Simulate fetching data from an EHR system
        await Task.Delay(100, cancellationToken);

        var resources = new List<Resource>();

        // ── Patient ──
        var patient = new Patient
        {
            Name = new List<HumanName>
            {
                new() { Family = "Smith", Given = new[] { "John" } }
            },
            Gender = AdministrativeGender.Male,
            BirthDate = "1985-03-15",
            Identifier = new List<Identifier>
            {
                new("urn:oid:2.16.840.1.113883.2.9.4.3.2", "EHR-PAT-001")
            },
            Active = true
        };
        resources.Add(patient);

        // ── Condition (Diabetes) ──
        var condition = new Condition
        {
            Code = new CodeableConcept("http://snomed.info/sct", "73211009", "Diabetes mellitus"),
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-clinical", "active"),
            VerificationStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-ver-status", "confirmed"),
            Onset = new FhirDateTime("2020-06-01")
        };
        resources.Add(condition);

        // ── Observation (Blood Glucose) ──
        var observation = new Observation
        {
            Status = ObservationStatus.Final,
            Code = new CodeableConcept("http://loinc.org", "15074-8", "Glucose [Moles/volume] in Blood"),
            Value = new Quantity(6.3m, "mmol/L", "http://unitsofmeasure.org"),
            Effective = new FhirDateTime("2024-01-15T08:30:00Z")
        };
        resources.Add(observation);

        _logger.LogInformation("[{Source}] Fetched {Count} resources", SourceName, resources.Count);
        return resources;
    }
}
