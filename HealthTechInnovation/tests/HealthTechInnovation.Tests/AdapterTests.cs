using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using IngestionService.Adapters;

namespace HealthTechInnovation.Tests;

public class AdapterTests
{
    [Fact]
    public async Task EhrAdapter_FetchDataAsync_ReturnsPatientConditionObservation()
    {
        // Arrange
        var logger = new Mock<ILogger<EhrAdapter>>();
        var adapter = new EhrAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();

        // Assert
        Assert.Equal(3, resources.Count);
        Assert.Contains(resources, r => r is Patient);
        Assert.Contains(resources, r => r is Condition);
        Assert.Contains(resources, r => r is Observation);
        Assert.Equal("EHR System", adapter.SourceName);
    }

    [Fact]
    public async Task EhrAdapter_FetchDataAsync_PatientHasCorrectName()
    {
        // Arrange
        var logger = new Mock<ILogger<EhrAdapter>>();
        var adapter = new EhrAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();
        var patient = resources.OfType<Patient>().First();

        // Assert
        Assert.Equal("Smith", patient.Name.First().Family);
        Assert.Contains("John", patient.Name.First().Given);
    }

    [Fact]
    public async Task IoTAdapter_FetchDataAsync_ReturnsVitalSignObservations()
    {
        // Arrange
        var logger = new Mock<ILogger<IoTAdapter>>();
        var adapter = new IoTAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();

        // Assert
        Assert.Equal(3, resources.Count);
        Assert.All(resources, r => Assert.IsType<Observation>(r));
        Assert.Equal("IoT Medical Devices", adapter.SourceName);
    }

    [Fact]
    public async Task IoTAdapter_FetchDataAsync_HeartRateHasCorrectCode()
    {
        // Arrange
        var logger = new Mock<ILogger<IoTAdapter>>();
        var adapter = new IoTAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();
        var heartRate = resources.OfType<Observation>()
            .First(o => o.Code.Coding.Any(c => c.Code == "8867-4"));

        // Assert
        Assert.Equal("8867-4", heartRate.Code.Coding.First().Code);
        var value = heartRate.Value as Quantity;
        Assert.NotNull(value);
        Assert.Equal(72, value.Value);
    }

    [Fact]
    public async Task ExternalSystemAdapter_FetchDataAsync_ReturnsMedicationAndAllergy()
    {
        // Arrange
        var logger = new Mock<ILogger<ExternalSystemAdapter>>();
        var adapter = new ExternalSystemAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();

        // Assert
        Assert.Equal(3, resources.Count);
        Assert.Contains(resources, r => r is Medication);
        Assert.Contains(resources, r => r is MedicationRequest);
        Assert.Contains(resources, r => r is AllergyIntolerance);
        Assert.Equal("External Healthcare System", adapter.SourceName);
    }

    [Fact]
    public async Task ExternalSystemAdapter_FetchDataAsync_AllergyHasPenicillin()
    {
        // Arrange
        var logger = new Mock<ILogger<ExternalSystemAdapter>>();
        var adapter = new ExternalSystemAdapter(logger.Object);

        // Act
        var resources = (await adapter.FetchDataAsync()).ToList();
        var allergy = resources.OfType<AllergyIntolerance>().First();

        // Assert
        Assert.Contains(allergy.Code.Coding, c => c.Code == "764146007");
        Assert.Equal(AllergyIntolerance.AllergyIntoleranceType.Allergy, allergy.Type);
    }

    [Fact]
    public async Task AllAdapters_SupportCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ehrLogger = new Mock<ILogger<EhrAdapter>>();
        var iotLogger = new Mock<ILogger<IoTAdapter>>();
        var extLogger = new Mock<ILogger<ExternalSystemAdapter>>();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            new EhrAdapter(ehrLogger.Object).FetchDataAsync(cts.Token));
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            new IoTAdapter(iotLogger.Object).FetchDataAsync(cts.Token));
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            new ExternalSystemAdapter(extLogger.Object).FetchDataAsync(cts.Token));
    }
}
