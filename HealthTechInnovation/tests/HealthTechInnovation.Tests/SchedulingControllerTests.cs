using Moq;
using Xunit;
using Shared.Fhir;
using HealthTechInnovation.ApiGateway.Controllers;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HealthTechInnovation.Tests;

public class SchedulingControllerTests
{
    private readonly Mock<IFhirCrudService> _mockFhirService;
    private readonly Mock<ILogger<SchedulingController>> _mockLogger;
    private readonly SchedulingController _controller;

    public SchedulingControllerTests()
    {
        _mockFhirService = new Mock<IFhirCrudService>();
        _mockLogger = new Mock<ILogger<SchedulingController>>();
        _controller = new SchedulingController(_mockFhirService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAppointments_ReturnsOk_WithBundle()
    {
        var bundle = new Bundle();
        _mockFhirService.Setup(s => s.SearchResourcesAsync<Appointment>(It.IsAny<string[]>()))
                        .ReturnsAsync(bundle);

        var result = await _controller.GetAppointments();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(bundle, okResult.Value);
    }
}
