using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessingService.Terminology;
using Shared.Fhir;


namespace HealthTechInnovation.Tests;

public class TerminologyServiceTests
{
    [Fact]
    public async Task EnrichCodeableConceptAsync_WithMissingDisplay_CallsLookupAndEnriches()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        
        // Mock $lookup response
        var lookupResult = new Parameters();
        lookupResult.Add("display", new FhirString("Heart rate"));
        lookupResult.Add("name", new FhirString("LOINC"));

        fhirService.Setup(c => c.TypeOperationAsync<Parameters>(
                It.IsAny<string>(), It.IsAny<Parameters>()))
            .ReturnsAsync(lookupResult);

        var logger = new Mock<ILogger<TerminologyService>>();
        var service = new TerminologyService(fhirService.Object, logger.Object);

        var concept = new CodeableConcept("http://loinc.org", "8867-4"); // No display

        // Act
        await service.EnrichCodeableConceptAsync(concept);

        // Assert
        Assert.Equal("Heart rate", concept.Coding[0].Display);
        // Assert
        Assert.Equal("Heart rate", concept.Coding[0].Display);
        fhirService.Verify(c => c.TypeOperationAsync<Parameters>(
            It.IsAny<string>(), It.IsAny<Parameters>()), Times.Once);
    }

    [Fact]
    public async Task ValidateCodeAsync_ValidCode_ReturnsTrue()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();

        // Mock $validate-code response
        var validateResult = new Parameters();
        validateResult.Add("result", new FhirBoolean(true));
        validateResult.Add("display", new FhirString("Valid Code"));

        fhirService.Setup(c => c.TypeOperationAsync<Parameters>(
                It.IsAny<string>(), It.IsAny<Parameters>()))
            .ReturnsAsync(validateResult);

        var logger = new Mock<ILogger<TerminologyService>>();
        var service = new TerminologyService(fhirService.Object, logger.Object);

        // Act
        var result = await service.ValidateCodeAsync("http://snomed.info/sct", "123456");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Valid Code", result.Display);
    }
}
