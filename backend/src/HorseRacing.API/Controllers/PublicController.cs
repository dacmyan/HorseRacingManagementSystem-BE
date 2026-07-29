using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HorseRacing.Domain.Entities;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.Notifications.DTOs;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.DTOs;
using HorseRacing.Application.Features.TournamentAndRacing.DTOs;
using HorseRacing.Application.Features.TournamentAndRacing.Services;
using HorseRacing.Application.Features.Public.Interfaces;
using HorseRacing.Application.Features.Public.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IRaceService _raceService;
    private readonly IRoundService _roundService;
    private readonly ITournamentService _tournamentService;
    private readonly IRaceResultService _resultService;
    private readonly IPublicQueryService _publicQueryService;

    public PublicController(
        INotificationService notificationService,
        IRaceService raceService,
        IRoundService roundService,
        ITournamentService tournamentService,
        IRaceResultService resultService,
        IPublicQueryService publicQueryService)
    {
        _notificationService = notificationService;
        _raceService = raceService;
        _roundService = roundService;
        _tournamentService = tournamentService;
        _resultService = resultService;
        _publicQueryService = publicQueryService;
    }

    private int GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameIdentifier))
        {
            nameIdentifier = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        }
        return int.TryParse(nameIdentifier, out var id) ? id : 0;
    }

    private bool IsAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
    
    private bool IsAllowedFullAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "Referee", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "HorseOwner", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "Spectator", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("rankings/jockeys")]
    [AllowAnonymous]
    public async Task<IActionResult> GetJockeyRankings()
    {
        try
        {
            var rankings = await _publicQueryService.GetJockeyRankingsAsync();
            return Ok(new { message = "Jockey rankings retrieved successfully", result = rankings });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving jockey rankings", detail = ex.Message });
        }
    }

    [HttpGet("rankings/horses")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHorseRankings()
    {
        try
        {
            var rankings = await _publicQueryService.GetHorseRankingsAsync();
            return Ok(new { message = "Horse rankings retrieved successfully", result = rankings });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving horse rankings", detail = ex.Message });
        }
    }

    [HttpGet("notifications")]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] string? type,
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            var notifications = await _notificationService.GetNotificationsForUserPagedAsync(userId, type, isRead, page, pageSize);
            return Ok(new { message = "Notifications retrieved successfully", result = notifications });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving notifications", detail = ex.Message });
        }
    }

    [HttpPut("notifications/{id}/read")]
    [Authorize]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _notificationService.MarkAsReadAsync(id, userId);
            return Ok(new { message = "Notification marked as read successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred updating notification", detail = ex.Message });
        }
    }

    [HttpPut("notifications/read-all")]
    [Authorize]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred updating notifications", detail = ex.Message });
        }
    }

    [HttpDelete("notifications/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _notificationService.DeleteNotificationAsync(id, userId);
            return Ok(new { message = "Notification soft deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred deleting notification", detail = ex.Message });
        }
    }

    [HttpGet("races/schedule")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicRaceSchedule()
    {
        try
        {
            var schedule = await _raceService.GetPublicRaceScheduleAsync();

            if (!IsAdmin())
            {
                var visibleSchedule = new List<RaceScheduleResponse>();
                foreach (var s in schedule)
                {
                    if (await _publicQueryService.IsTournamentVisibleAsync(s.TournamentId, false))
                    {
                        visibleSchedule.Add(s);
                    }
                }
                schedule = visibleSchedule;
            }

            return Ok(new { message = "Public race schedule retrieved successfully", result = schedule });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "An error occurred retrieving public race schedule" });
        }
    }

    [HttpGet("tournaments/{tournamentId}/rounds")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoundsByTournament(long tournamentId)
    {
        var tournament = await _tournamentService.GetTournamentByIdAsync(tournamentId);
        if (tournament == null)
        {
            return NotFound(new { message = $"Tournament with ID {tournamentId} was not found." });
        }

        if (!await _publicQueryService.IsTournamentVisibleAsync(tournamentId, IsAdmin()))
        {
            return NotFound(new { message = $"Tournament with ID {tournamentId} was not found." });
        }

        var rounds = await _roundService.GetRoundsByTournamentIdAsync(tournamentId);
        if (rounds == null)
        {
            return NotFound(new { message = $"Tournament with ID {tournamentId} was not found." });
        }

        return Ok(new { message = "Rounds retrieved successfully", result = rounds });
    }

    [HttpGet("rounds/{roundId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoundDetail(long roundId)
    {
        var round = await _roundService.GetRoundByIdAsync(roundId);
        if (round == null)
        {
            return NotFound(new { message = $"Round with ID {roundId} was not found." });
        }

        if (!await _publicQueryService.IsTournamentVisibleAsync(round.TournamentId, IsAdmin()))
        {
            return NotFound(new { message = $"Round with ID {roundId} was not found." });
        }

        return Ok(new { message = "Round details retrieved successfully", result = round });
    }

    [HttpGet("tournaments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTournaments()
    {
        try
        {
            var result = await _publicQueryService.GetTournamentsAsync(IsAllowedFullAccess());
            return Ok(new { message = "Tournaments retrieved successfully", result = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving tournaments", detail = ex.Message });
        }
    }

    [HttpGet("tournaments/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTournamentDetail(long id)
    {
        try
        {
            var result = await _publicQueryService.GetTournamentDetailAsync(id, IsAllowedFullAccess());
            if (result == null)
            {
                return NotFound(new { message = $"Tournament with ID {id} was not found." });
            }
            return Ok(new { message = "Tournament details retrieved successfully", result = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving tournament details", detail = ex.Message });
        }
    }

    [HttpGet("tournaments/{id}/qualified-horses")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQualifiedHorses(long id)
    {
        try
        {
            var result = await _tournamentService.GetQualifiedHorsesAsync(id);
            return Ok(new { message = "Qualified horses retrieved successfully", result = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving qualified horses", detail = ex.Message });
        }
    }

    [HttpGet("races/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRaceDetail(long id)
    {
        try
        {
            var race = await _raceService.GetRaceByIdAsync(id);
            if (race == null)
            {
                return NotFound(new { message = $"Race with ID {id} was not found." });
            }

            if (!await _publicQueryService.IsTournamentVisibleAsync(race.TournamentId, IsAdmin()))
            {
                return NotFound(new { message = $"Race with ID {id} was not found." });
            }

            return Ok(new { message = "Race details retrieved successfully", result = race });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving race details", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/entries")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRaceEntries(long raceId)
    {
        try
        {
            var race = await _raceService.GetRaceByIdAsync(raceId);
            if (race == null || !await _publicQueryService.IsTournamentVisibleAsync(race.TournamentId, IsAdmin()))
            {
                 return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }

            var entries = await _raceService.GetRaceEntriesByRaceIdAsync(raceId);
            if (entries == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }

            return Ok(new { message = "Race entries retrieved successfully", result = entries });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving race entries", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/results")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRaceResults(long raceId)
    {
        try
        {
            var race = await _raceService.GetRaceByIdAsync(raceId);
            if (race == null || !await _publicQueryService.IsTournamentVisibleAsync(race.TournamentId, IsAdmin()))
            {
                 return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }

            var response = await _resultService.GetPublicResultsByRaceIdAsync(raceId);
            if (response == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }

            return Ok(new { message = "Race results retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving race results", detail = ex.Message });
        }
    }

    [HttpGet("races/live")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLiveRaces()
    {
        try
        {
            var liveRaces = await _publicQueryService.GetLiveRacesAsync();
            return Ok(new { message = "Live races retrieved successfully", result = liveRaces });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving live races", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{id}/generate-races")]
    [AllowAnonymous]
    public async Task<IActionResult> GenerateRacesForTournament(long id)
    {
        try
        {
            var races = await _tournamentService.GenerateRacesForTournamentAsync(id);
            return Ok(new { message = "Races generated successfully", result = races });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred", detail = ex.Message });
        }
    }
}
