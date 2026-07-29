using System;
using System.Threading.Tasks;
using HorseRacing.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IDemoService _demoService;

    public DemoController(IDemoService demoService)
    {
        _demoService = demoService;
    }

    [HttpPost("auto-setup")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetupDemoTournament()
    {
        try
        {
            var tournament = await _demoService.SetupDemoTournamentAsync();
            return Ok(new 
            { 
                Message = "Demo tournament setup successfully with 12 horses and jockeys.", 
                TournamentId = tournament.TournamentId,
                TournamentName = tournament.Name 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "An error occurred during demo setup.", Details = ex.Message });
        }
    }
}
