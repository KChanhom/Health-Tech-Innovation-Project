using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Fhir;

namespace HealthTechInnovation.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatientController : ControllerBase
{
    private readonly IFhirCrudService _fhirService;
    private readonly ILogger<PatientController> _logger;

    public PatientController(IFhirCrudService fhirService, ILogger<PatientController> logger)
    {
        _fhirService = fhirService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<Bundle>> GetPatients()
    {
        _logger.LogInformation("Getting all patients");
        var result = await _fhirService.SearchPatientsAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Patient>> GetPatient(string id)
    {
        _logger.LogInformation("Getting patient with ID: {Id}", id);
        var result = await _fhirService.ReadPatientAsync(id);
        
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Patient>> CreatePatient([FromBody] Patient patient)
    {
        _logger.LogInformation("Creating new patient");
        var result = await _fhirService.CreatePatientAsync(patient);
        return CreatedAtAction(nameof(GetPatient), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Patient>> UpdatePatient(string id, [FromBody] Patient patient)
    {
        if (id != patient.Id)
        {
            return BadRequest("ID mismatch");
        }

        _logger.LogInformation("Updating patient with ID: {Id}", id);
        try
        {
            var result = await _fhirService.UpdatePatientAsync(patient);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating patient {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(string id)
    {
        _logger.LogInformation("Deleting patient with ID: {Id}", id);
        await _fhirService.DeletePatientAsync(id);
        return NoContent();
    }
}
