using Microsoft.AspNetCore.Mvc;
using HorseRacing.Application.Features.Public.Interfaces;
using System.Threading.Tasks;
using System;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IPublicQueryService _publicQueryService;

    public HealthController(IPublicQueryService publicQueryService)
    {
        _publicQueryService = publicQueryService;
    }

    [HttpGet("db")]
    public async Task<IActionResult> TestDbConnection()
    {
        try
        {
            var canConnect = await _publicQueryService.CheckDatabaseHealthAsync();
            if (canConnect)
            {
                return Ok(new { status = "success", message = "Database connected successfully" });
            }
            return StatusCode(500, new { status = "error", message = "Cannot connect to database", detail = "Database creator check returned false" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Cannot connect to database", detail = ex.Message });
        }
    }
}
