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

    [HttpPost("start-race/{tournamentId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> StartDemoTournament(long tournamentId)
    {
        try
        {
            var tournament = await _demoService.StartDemoTournamentAsync(tournamentId);
            return Ok(new 
            { 
                Message = "Demo tournament started successfully. Ready for manual referee input.", 
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

    [HttpPost("populate-tournament/{tournamentId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PopulateTournament(long tournamentId)
    {
        try
        {
            var tournament = await _demoService.PopulateTournamentAsync(tournamentId);
            return Ok(new 
            { 
                Message = "Tournament populated successfully with 12 entries.", 
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
            return StatusCode(500, new { message = "An error occurred during tournament population.", detail = ex.Message });
        }
    }

    [HttpPost("start-single-race/{raceId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> StartSingleRace(long raceId)
    {
        try
        {
            var race = await _demoService.StartSingleRaceAsync(raceId);
            return Ok(new 
            { 
                Message = "Single race started successfully. Ready for manual referee input.", 
                RaceId = race.RaceId,
                Status = race.Status 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during single race resolution.", detail = ex.Message });
        }
    }
}
