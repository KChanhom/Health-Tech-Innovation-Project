using Moq;
using Xunit;
using Shared.Fhir;
using HealthTechInnovation.ApiGateway.Controllers;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HealthTechInnovation.Tests;

public class ObservationControllerTests
{
    private readonly Mock<IFhirCrudService> _mockFhirService;
    private readonly Mock<ILogger<ObservationController>> _mockLogger;
    private readonly ObservationController _controller;

    public ObservationControllerTests()
    {
        _mockFhirService = new Mock<IFhirCrudService>();
        _mockLogger = new Mock<ILogger<ObservationController>>();
        _controller = new ObservationController(_mockFhirService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetObservations_ReturnsOk_WithBundle()
    {
        var bundle = new Bundle();
        _mockFhirService.Setup(s => s.SearchResourcesAsync<Observation>(It.IsAny<string[]>()))
                        .ReturnsAsync(bundle);

        var result = await _controller.GetObservations();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(bundle, okResult.Value);
    }
}
