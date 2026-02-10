using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Fhir;
using Task = System.Threading.Tasks.Task;

namespace HealthTechInnovation.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<SchedulingController> _logger;

    public SchedulingController(IFhirCrudService fhirService, ILogger<SchedulingController> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<Bundle>> GetAppointments()
    {
        _logger.LogInformation("Getting all appointments");
        var result = await _fhirService.SearchResourcesAsync<Appointment>();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Appointment>> GetAppointment(string id)
    {
        _logger.LogInformation("Getting appointment with ID: {Id}", id);
        var result = await _fhirService.ReadResourceAsync<Appointment>(id);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Appointment>> CreateAppointment([FromBody] Appointment appointment)
    {
        _logger.LogInformation("Creating new appointment");
        var result = await _fhirService.CreateResourceAsync(appointment);
        return CreatedAtAction(nameof(GetAppointment), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Appointment>> UpdateAppointment(string id, [FromBody] Appointment appointment)
    {
        if (id != appointment.Id)
        {
            return BadRequest("ID mismatch");
        }

        _logger.LogInformation("Updating appointment with ID: {Id}", id);
        try
        {
            var result = await _fhirService.UpdateResourceAsync(appointment);
            return Ok(result);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error updating appointment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
