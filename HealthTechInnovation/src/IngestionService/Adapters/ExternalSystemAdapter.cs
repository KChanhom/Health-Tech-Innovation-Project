using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace IngestionService.Adapters;

/// <summary>
/// Adapter for external healthcare systems (e.g., pharmacy, lab).
/// Fetches Medication and AllergyIntolerance resources.
/// </summary>
public class ExternalSystemAdapter : IDataSourceAdapter
{
    private readonly ILogger<ExternalSystemAdapter> _logger;

    public string SourceName => "External Healthcare System";

    public ExternalSystemAdapter(ILogger<ExternalSystemAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<Resource>> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Source}] Fetching data from external system...", SourceName);

        // Simulate fetching data from an external pharmacy/lab system
        await Task.Delay(75, cancellationToken);

        var resources = new List<Resource>();

        // ── Medication (Metformin) ──
        var medication = new Medication
        {
            Code = new CodeableConcept("http://www.nlm.nih.gov/research/umls/rxnorm", "860975", "Metformin 500 MG Oral Tablet"),
            Status = Medication.MedicationStatusCodes.Active,
            Form = new CodeableConcept("http://snomed.info/sct", "385055001", "Tablet dose form")
        };
        resources.Add(medication);

        // ── MedicationRequest ──
        var medicationRequest = new MedicationRequest
        {
            Status = MedicationRequest.MedicationrequestStatus.Active,
            Intent = MedicationRequest.MedicationRequestIntent.Order,
            Medication = new CodeableConcept("http://www.nlm.nih.gov/research/umls/rxnorm", "860975", "Metformin 500 MG"),
            DosageInstruction = new List<Dosage>
            {
                new()
                {
                    Text = "Take one tablet twice daily with meals",
                    Timing = new Timing
                    {
                        Repeat = new Timing.RepeatComponent { Frequency = 2, Period = 1, PeriodUnit = Timing.UnitsOfTime.D }
                    }
                }
            },
            AuthoredOn = "2024-01-10"
        };
        resources.Add(medicationRequest);

        // ── AllergyIntolerance (Penicillin) ──
        var allergy = new AllergyIntolerance
        {
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", "active"),
            VerificationStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification", "confirmed"),
            Type = AllergyIntolerance.AllergyIntoleranceType.Allergy,
            Category = new List<AllergyIntolerance.AllergyIntoleranceCategory?>
            {
                AllergyIntolerance.AllergyIntoleranceCategory.Medication
            },
            Code = new CodeableConcept("http://snomed.info/sct", "764146007", "Penicillin"),
            Reaction = new List<AllergyIntolerance.ReactionComponent>
            {
                new()
                {
                    Manifestation = new List<CodeableConcept>
                    {
                        new("http://snomed.info/sct", "271807003", "Skin rash")
                    },
                    Severity = AllergyIntolerance.AllergyIntoleranceSeverity.Moderate
                }
            }
        };
        resources.Add(allergy);

        _logger.LogInformation("[{Source}] Fetched {Count} resources", SourceName, resources.Count);
        return resources;
    }
}
