using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace IngestionService.Hl7v2;

/// <summary>
/// Transforms HL7 v2 messages (pipe-delimited) into FHIR resources.
/// Intended to run before publishing the resources to Kafka or other message buses.
/// </summary>
public interface IHL7v2ToFhirTransformer
{
    /// <summary>
    /// Parses a single HL7 v2 message string and produces one or more FHIR resources.
    /// </summary>
    /// <param name="hl7Message">Raw HL7 v2 message text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of FHIR resources derived from the message.</returns>
    Task<IReadOnlyList<Resource>> TransformAsync(
        string hl7Message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic implementation of <see cref="IHL7v2ToFhirTransformer"/> that understands a subset of HL7 v2:
/// - MSH: metadata only (currently not mapped to a resource)
/// - PID: maps to a FHIR Patient
/// - OBX: maps to one or more FHIR Observation resources
/// </summary>
public class Hl7v2ToFhirTransformer : IHL7v2ToFhirTransformer
{
    private readonly ILogger<Hl7v2ToFhirTransformer> _logger;

    public Hl7v2ToFhirTransformer(ILogger<Hl7v2ToFhirTransformer> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<Resource>> TransformAsync(
        string hl7Message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hl7Message))
        {
            return Task.FromResult<IReadOnlyList<Resource>>(Array.Empty<Resource>());
        }

        var resources = new List<Resource>();
        Patient? patient = null;
        var observations = new List<Observation>();

        var lines = hl7Message
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fields = line.Split('|');
            if (fields.Length == 0)
            {
                continue;
            }

            var segment = fields[0].Trim().ToUpperInvariant();

            switch (segment)
            {
                case "MSH":
                    // Currently we only log basic metadata from MSH.
                    HandleMsh(fields);
                    break;

                case "PID":
                    patient = HandlePid(fields);
                    if (patient != null)
                    {
                        resources.Add(patient);
                    }
                    break;

                case "OBX":
                    var obxObservation = HandleObx(fields);
                    if (obxObservation != null)
                    {
                        observations.Add(obxObservation);
                        resources.Add(obxObservation);
                    }
                    break;
            }
        }

        // If we have both a patient and observations, link them by subject.
        if (patient != null)
        {
            foreach (var obs in observations)
            {
                if (obs.Subject == null)
                {
                    obs.Subject = new ResourceReference
                    {
                        Reference = $"Patient/{patient.Id}",
                        Display = patient.Name?.FirstOrDefault()?.ToString()
                    };
                }
            }
        }

        _logger.LogInformation(
            "Transformed HL7 v2 message into {ResourceCount} FHIR resources " +
            "({PatientCount} Patient, {ObservationCount} Observation)",
            resources.Count,
            patient != null ? 1 : 0,
            observations.Count);

        return Task.FromResult<IReadOnlyList<Resource>>(resources);
    }

    private void HandleMsh(string[] fields)
    {
        // MSH|^~\&|SENDING_APP|SENDING_FAC|RECEIVING_APP|RECEIVING_FAC|20240115101010||ORU^R01|MSGID1234|P|2.5.1
        // We currently just log some metadata for observability.
        var sendingApp = GetField(fields, 2);
        var sendingFacility = GetField(fields, 3);
        var messageType = GetField(fields, 8);
        var controlId = GetField(fields, 9);
        var version = GetField(fields, 11);

        _logger.LogDebug(
            "Parsed MSH - SendingApp={SendingApp}, SendingFacility={SendingFacility}, " +
            "MessageType={MessageType}, ControlId={ControlId}, Version={Version}",
            sendingApp, sendingFacility, messageType, controlId, version);
    }

    private Patient? HandlePid(string[] fields)
    {
        // Example:
        // PID|1||12345^^^HOSP^MR||Doe^John||19800101|M|||123 Main St^^Metropolis^NY^10001||555-1234
        var patientId = ParseCxIdentifier(GetField(fields, 3)) ?? GetField(fields, 2);
        var nameField = GetField(fields, 5);
        var birthDateField = GetField(fields, 7);
        var genderField = GetField(fields, 8);

        var patient = new Patient
        {
            Identifier = new List<Identifier>()
        };

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            patient.Identifier.Add(new Identifier("urn:hl7v2:pid", patientId));
        }

        if (!string.IsNullOrWhiteSpace(nameField))
        {
            var nameParts = nameField.Split('^');
            var family = nameParts.ElementAtOrDefault(0);
            var given = nameParts.ElementAtOrDefault(1);
            patient.Name = new List<HumanName>
            {
                new()
                {
                    Family = string.IsNullOrWhiteSpace(family) ? null : family,
                    Given = string.IsNullOrWhiteSpace(given)
                        ? new List<string>()
                        : new List<string> { given }
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(birthDateField) &&
            DateTime.TryParseExact(
                birthDateField,
                new[] { "yyyyMMdd", "yyyyMMddHHmmss" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var birthDate))
        {
            patient.BirthDateElement = new Date(birthDate.ToString("yyyy-MM-dd"));
        }

        if (!string.IsNullOrWhiteSpace(genderField))
        {
            patient.Gender = genderField.ToUpperInvariant() switch
            {
                "M" => AdministrativeGender.Male,
                "F" => AdministrativeGender.Female,
                "O" => AdministrativeGender.Other,
                "U" => AdministrativeGender.Unknown,
                _ => AdministrativeGender.Unknown
            };
        }

        // Let the FHIR server assign ID on create; we do not set patient.Id here.
        return patient;
    }

    private Observation? HandleObx(string[] fields)
    {
        // Example:
        // OBX|1|NM|8867-4^Heart rate^LN||72|/min|60-100|N|||F
        var setId = GetField(fields, 1);
        var valueType = GetField(fields, 2);
        var identifierField = GetField(fields, 3);
        var valueField = GetField(fields, 5);
        var unitsField = GetField(fields, 6);
        var statusField = GetField(fields, 11);

        var obs = new Observation
        {
            Status = ObservationStatus.Final,
            Identifier = new List<Identifier>()
        };

        if (!string.IsNullOrWhiteSpace(setId))
        {
            obs.Identifier.Add(new Identifier("urn:hl7v2:obx:setid", setId));
        }

        if (!string.IsNullOrWhiteSpace(identifierField))
        {
            // CE/CWE format: <identifier>^<text>^<codingSystem>
            var parts = identifierField.Split('^');
            var code = parts.ElementAtOrDefault(0);
            var text = parts.ElementAtOrDefault(1);
            var system = parts.ElementAtOrDefault(2);

            obs.Code = new CodeableConcept(
                string.IsNullOrWhiteSpace(system) ? null : MapCodeSystem(system),
                string.IsNullOrWhiteSpace(code) ? null : code,
                string.IsNullOrWhiteSpace(text) ? null : text);
        }

        if (!string.IsNullOrWhiteSpace(valueType))
        {
            obs.Value = valueType.ToUpperInvariant() switch
            {
                "NM" => CreateQuantityValue(valueField, unitsField),
                "ST" or "TX" => new FhirString(valueField),
                _ => string.IsNullOrWhiteSpace(valueField) ? null : new FhirString(valueField)
            };
        }

        if (!string.IsNullOrWhiteSpace(statusField))
        {
            obs.Status = statusField.ToUpperInvariant() switch
            {
                "F" => ObservationStatus.Final,
                "P" => ObservationStatus.Preliminary,
                "C" => ObservationStatus.Corrected,
                "X" => ObservationStatus.Cancelled,
                _ => ObservationStatus.Unknown
            };
        }

        return obs;
    }

    private static Quantity? CreateQuantityValue(string? valueField, string? unitsField)
    {
        if (string.IsNullOrWhiteSpace(valueField))
        {
            return null;
        }

        if (!decimal.TryParse(
                valueField,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            return null;
        }

        string? code = null;
        string? display = null;
        string? system = "http://unitsofmeasure.org";

        if (!string.IsNullOrWhiteSpace(unitsField))
        {
            // Simple case: units as plain text (e.g. "/min", "mmHg")
            // or HL7 CE format: <identifier>^<text>^<codingSystem>
            var parts = unitsField.Split('^');
            if (parts.Length == 1)
            {
                code = parts[0];
                display = parts[0];
            }
            else
            {
                code = parts.ElementAtOrDefault(0);
                display = parts.ElementAtOrDefault(1) ?? code;
                var hl7System = parts.ElementAtOrDefault(2);
                if (!string.IsNullOrWhiteSpace(hl7System))
                {
                    system = MapCodeSystem(hl7System);
                }
            }
        }

        return new Quantity(value, code, system)
        {
            Unit = display
        };
    }

    private static string GetField(string[] fields, int index)
    {
        return index >= 0 && index < fields.Length ? fields[index] : string.Empty;
    }

    private static string MapCodeSystem(string hl7CodeSystem)
    {
        // Very small mapping for common systems used in observations.
        return hl7CodeSystem.ToUpperInvariant() switch
        {
            "LN" or "LOINC" => "http://loinc.org",
            "SNM" or "SNOMED" or "SCT" or "SNOMED-CT" => "http://snomed.info/sct",
            _ => hl7CodeSystem
        };
    }
}

