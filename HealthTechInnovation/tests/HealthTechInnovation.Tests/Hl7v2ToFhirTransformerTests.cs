using IngestionService.Hl7v2;
using Microsoft.Extensions.Logging;
using Moq;

namespace HealthTechInnovation.Tests;

public class Hl7v2ToFhirTransformerTests
{
    [Fact]
    public async Task TransformAsync_EmptyMessage_ReturnsEmptyList()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        // Act
        var result = await transformer.TransformAsync(string.Empty);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task TransformAsync_PidOnly_ProducesSinglePatient()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        var message = string.Join('\n', new[]
        {
            "MSH|^~\\&|APP|FAC|RECAPP|RECFAC|20240115101010||ADT^A01|MSGID1234|P|2.5.1",
            "PID|1||12345^^^HOSP^MR||Doe^John||19800101|M"
        });

        // Act
        var result = await transformer.TransformAsync(message);

        // Assert
        var patient = Assert.Single(result).ShouldBeOfType<Patient>();
        Assert.Equal("Doe", patient.Name.First().Family);
        Assert.Contains("John", patient.Name.First().Given);
        Assert.Equal(AdministrativeGender.Male, patient.Gender);
        Assert.Contains(patient.Identifier, id => id.System == "urn:hl7v2:pid" && id.Value == "12345");
    }

    [Fact]
    public async Task TransformAsync_PidWithInvalidBirthdate_DoesNotSetBirthDate()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        var message = "PID|1||12345^^^HOSP^MR||Doe^John||not-a-date|M";

        // Act
        var result = await transformer.TransformAsync(message);

        // Assert
        var patient = Assert.Single(result).ShouldBeOfType<Patient>();
        Assert.Null(patient.BirthDateElement);
    }

    [Fact]
    public async Task TransformAsync_PidAndObx_ProducesPatientAndObservationWithLink()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        var message = string.Join('\n', new[]
        {
            "MSH|^~\\&|APP|FAC|RECAPP|RECFAC|20240115101010||ORU^R01|MSGID1234|P|2.5.1",
            "PID|1||12345^^^HOSP^MR||Doe^John||19800101|M",
            "OBX|1|NM|8867-4^Heart rate^LN||72|/min|||N|||F"
        });

        // Act
        var result = (await transformer.TransformAsync(message)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        var patient = result.OfType<Patient>().Single();
        var observation = result.OfType<Observation>().Single();

        Assert.NotNull(observation.Subject);
        Assert.StartsWith("Patient/", observation.Subject.Reference);
        Assert.Equal("8867-4", observation.Code.Coding.First().Code);

        var quantity = Assert.IsType<Quantity>(observation.Value);
        Assert.Equal(72, quantity.Value);
        Assert.Equal("/min", quantity.Unit);
    }

    [Fact]
    public async Task TransformAsync_ObxWithInvalidNumericValue_SetsNullValue()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        var message = "OBX|1|NM|8867-4^Heart rate^LN||not-a-number|/min|||N|||F";

        // Act
        var result = await transformer.TransformAsync(message);

        // Assert
        var observation = Assert.Single(result).ShouldBeOfType<Observation>();
        Assert.Null(observation.Value);
    }

    [Fact]
    public async Task TransformAsync_CancellationRequested_ThrowsTaskCanceled()
    {
        // Arrange
        var logger = new Mock<ILogger<Hl7v2ToFhirTransformer>>();
        var transformer = new Hl7v2ToFhirTransformer(logger.Object);

        var message = string.Join('\n', Enumerable.Repeat("OBX|1|TX|123^Test^LN||some text", 10));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            transformer.TransformAsync(message, cts.Token));
    }
}

