using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Fhir;

namespace HealthTechInnovation.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ObservationController : ControllerBase
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<ObservationController> _logger;

    public ObservationController(IFhirCrudService fhirService, ILogger<ObservationController> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<Bundle>> GetObservations()
    {
        _logger.LogInformation("Getting all observations");
        var result = await _fhirService.SearchResourcesAsync<Observation>();
        return Ok(result);
    }

    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<Bundle>> GetObservationsByPatient(string patientId)
    {
        _logger.LogInformation("Getting observations for patient: {PatientId}", patientId);
        var result = await _fhirService.SearchResourcesAsync<Observation>(new[] { $"subject=Patient/{patientId}" });
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Observation>> GetObservation(string id)
    {
        _logger.LogInformation("Getting observation with ID: {Id}", id);
        var result = await _fhirService.ReadResourceAsync<Observation>(id);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Observation>> CreateObservation([FromBody] Observation observation)
    {
        _logger.LogInformation("Creating new observation");
        var result = await _fhirService.CreateResourceAsync(observation);
        return CreatedAtAction(nameof(GetObservation), new { id = result.Id }, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteObservation(string id)
    {
        _logger.LogInformation("Deleting observation with ID: {Id}", id);
        await _fhirService.DeleteResourceAsync("Observation", id);
        return NoContent();
    }
}
