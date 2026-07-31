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

    [HttpPost("setup-race")]
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
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during demo setup.", detail = ex.Message });
        }
    }

    [HttpPost("resolve-race/{tournamentId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveDemoTournament(long tournamentId)
    {
        try
        {
            var tournament = await _demoService.ResolveDemoTournamentAsync(tournamentId);
            return Ok(new 
            { 
                Message = "Demo tournament resolved successfully. Betting payouts triggered.", 
                TournamentId = tournament.TournamentId,
                Status = tournament.Status 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during demo resolution.", detail = ex.Message });
        }
    }
}
