using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessingService.Persistence;
using Shared.Fhir;


namespace HealthTechInnovation.Tests;

public class ResourcePersistenceServiceTests
{
    [Fact]
    public async Task SaveResourceAsync_NewResource_CreatesIt()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        
        var createdPatient = new Patient { Id = "new-id" };
        fhirService.Setup(c => c.CreateResourceAsync(It.IsAny<Resource>()))
            .ReturnsAsync(createdPatient);

        var logger = new Mock<ILogger<ResourcePersistenceService>>();
        var service = new ResourcePersistenceService(fhirService.Object, logger.Object);

        var patient = new Patient(); // No ID

        // Act
        var result = await service.SaveResourceAsync(patient);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Create", result.Operation);
        Assert.Equal("new-id", result.ResourceId);
        fhirService.Verify(c => c.CreateResourceAsync(It.IsAny<Resource>()), Times.Once);
    }

    [Fact]
    public async Task SaveResourceAsync_ExistingResource_UpdatesIt()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        
        var updatedPatient = new Patient { Id = "existing-id" };
        fhirService.Setup(c => c.UpdateResourceAsync(It.IsAny<Resource>()))
            .ReturnsAsync(updatedPatient);

        var logger = new Mock<ILogger<ResourcePersistenceService>>();
        var service = new ResourcePersistenceService(fhirService.Object, logger.Object);

        var patient = new Patient { Id = "existing-id" };

        // Act
        var result = await service.SaveResourceAsync(patient);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Update", result.Operation);
        Assert.Equal("existing-id", result.ResourceId);
        fhirService.Verify(c => c.UpdateResourceAsync(It.IsAny<Resource>()), Times.Once);
    }

    [Fact]
    public async Task SaveBatchAsync_SendsTransactionBundle()
    {
        // Arrange
        var fhirService = new Mock<IFhirCrudService>();
        
        var responseBundle = new Bundle
        {
            Type = Bundle.BundleType.TransactionResponse,
            Entry = new List<Bundle.EntryComponent>
            {
                new() { Response = new Bundle.ResponseComponent { Status = "201 Created" }, Resource = new Patient { Id = "1" } },
                new() { Response = new Bundle.ResponseComponent { Status = "200 OK" }, Resource = new Observation { Id = "2" } }
            }
        };

        fhirService.Setup(c => c.TransactionAsync(It.IsAny<Bundle>()))
            .ReturnsAsync(responseBundle);

        var logger = new Mock<ILogger<ResourcePersistenceService>>();
        var service = new ResourcePersistenceService(fhirService.Object, logger.Object);

        var resources = new List<Resource>
        {
            new Patient(), // Create
            new Observation { Id = "obs-1" } // Update
        };

        // Act
        var result = await service.SaveBatchAsync(resources);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, result.TotalCount);
        fhirService.Verify(c => c.TransactionAsync(It.Is<Bundle>(b => 
            b.Type == Bundle.BundleType.Transaction && b.Entry.Count == 2)), Times.Once);
    }
}
