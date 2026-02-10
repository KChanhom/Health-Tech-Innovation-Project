using Moq;
using Xunit;
using Shared.Fhir;
using HealthTechInnovation.ApiGateway.Controllers;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HealthTechInnovation.Tests;

public class PatientControllerTests
{
    private readonly Mock<IFhirCrudService> _mockFhirService;
    private readonly Mock<ILogger<PatientController>> _mockLogger;
    private readonly PatientController _controller;

    public PatientControllerTests()
    {
        _mockFhirService = new Mock<IFhirCrudService>();
        _mockLogger = new Mock<ILogger<PatientController>>();
        _controller = new PatientController(_mockFhirService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetPatients_ReturnsOk_WithBundle()
    {
        // Arrange
        var bundle = new Bundle();
        _mockFhirService.Setup(s => s.SearchPatientsAsync(It.IsAny<string[]>()))
                        .ReturnsAsync(bundle);

        // Act
        var result = await _controller.GetPatients();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(bundle, okResult.Value);
    }

    [Fact]
    public async Task GetPatient_ReturnsOk_WhenFound()
    {
        // Arrange
        var patient = new Patient { Id = "123" };
        _mockFhirService.Setup(s => s.ReadPatientAsync("123"))
                        .ReturnsAsync(patient);

        // Act
        var result = await _controller.GetPatient("123");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(patient, okResult.Value);
    }

    [Fact]
    public async Task GetPatient_ReturnsNotFound_WhenNull()
    {
        // Arrange
        _mockFhirService.Setup(s => s.ReadPatientAsync("123"))
                        .ReturnsAsync((Patient?)null);

        // Act
        var result = await _controller.GetPatient("123");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreatePatient_ReturnsCreated()
    {
        // Arrange
        var patient = new Patient();
        var createdPatient = new Patient { Id = "123" };
        _mockFhirService.Setup(s => s.CreatePatientAsync(patient))
                        .ReturnsAsync(createdPatient);

        // Act
        var result = await _controller.CreatePatient(patient);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(createdPatient, createdResult.Value);
        Assert.Equal("GetPatient", createdResult.ActionName);
        Assert.Equal("123", createdResult.RouteValues?["id"]);
    }
}
