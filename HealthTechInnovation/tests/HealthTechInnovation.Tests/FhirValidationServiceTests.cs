using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessingService.Validation;
using Shared.Fhir;

namespace HealthTechInnovation.Tests;

public class FhirValidationServiceTests
{
    [Fact]
    public void ValidateLocally_ValidPatient_ReturnsSuccess()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        var logger = new Mock<ILogger<FhirValidationService>>();
        var service = new FhirValidationService(fhirService.Object, logger.Object);

        var patient = new Patient
        {
            Name = new List<HumanName> { new() { Family = "Doe", Given = new[] { "John" } } },
            Gender = AdministrativeGender.Male
        };

        // Act
        var result = service.ValidateLocally(patient);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateLocally_PatientWithoutName_ReturnsFailure()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        var logger = new Mock<ILogger<FhirValidationService>>();
        var service = new FhirValidationService(fhirService.Object, logger.Object);

        var patient = new Patient
        {
            Gender = AdministrativeGender.Male
            // Name missing
        };

        // Act
        var result = service.ValidateLocally(patient);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("Patient has no name"));
    }

    [Fact]
    public void ValidateLocally_ObservationWithoutStatus_ReturnsFailure()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        var logger = new Mock<ILogger<FhirValidationService>>();
        var service = new FhirValidationService(fhirService.Object, logger.Object);

        var observation = new Observation
        {
            Code = new CodeableConcept("http://loinc.org", "1234-5")
            // Status missing
        };

        // Act
        var result = service.ValidateLocally(observation);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Message.Contains("status is required"));
    }
}
