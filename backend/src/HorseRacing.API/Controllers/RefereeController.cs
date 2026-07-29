using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HorseRacing.Application.Features.Notifications.Interfaces;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/referee")]
[Authorize(Roles = "Referee")]
public class RefereeController : ControllerBase
{
    private readonly IRefereeService _refereeService;
    private readonly IRaceResultService _resultService;
    private readonly INotificationService _notificationService;

    public RefereeController(IRefereeService refereeService, IRaceResultService resultService, INotificationService notificationService)
    {
        _refereeService = refereeService;
        _resultService = resultService;
        _notificationService = notificationService;
    }

    private int GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameIdentifier))
        {
            nameIdentifier = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        }
        return int.Parse(nameIdentifier ?? "0");
    }

    [HttpPost("violations")]
    public async Task<IActionResult> LogViolation([FromBody] LogViolationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var refereeId = await _refereeService.GetRefereeIdByUserIdAsync(userId);
            request.RefereeId = (int)refereeId;
            
            var response = await _refereeService.LogViolationAsync(request);
            try
            {
                await _notificationService.SendNotificationToRoleAsync(
                    "Admin", "New race violation",
                    $"Referee '{userId}' recorded a violation for race #{request.RaceId}.",
                    "Violation", response.ViolationId, actionUrl: "/admin/violations");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATION ERROR] Failed to notify admins about violation {response.ViolationId}: {ex.Message}");
            }
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred logging the violation", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/violations")]
    public async Task<IActionResult> GetRaceViolations([FromRoute] long raceId)
    {
        try
        {
            var response = await _refereeService.GetViolationsByRaceIdAsync(raceId);
            if (response == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred retrieving race violations", detail = ex.Message });
        }
    }

    [HttpGet("violations")]
    public async Task<IActionResult> GetViolations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var violations = await _refereeService.GetViolationsAsync(userId);
            return Ok(new { message = "Violations retrieved successfully", result = violations });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving violations", detail = ex.Message });
        }
    }

    [HttpPost("reports")]
    public async Task<IActionResult> SubmitReport([FromBody] CreateRefereeReportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var refereeId = await _refereeService.GetRefereeIdByUserIdAsync(userId);
            request.RefereeId = (int)refereeId;

            var response = await _refereeService.SubmitReportAsync(request);
            try
            {
                await _notificationService.SendNotificationToRoleAsync(
                    "Admin", "New referee report",
                    $"A referee report was submitted for race #{response.RaceId}.",
                    "System", checked((int)response.ReportId), actionUrl: "/admin/reports");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATION ERROR] Failed to notify admins about report {response.ReportId}: {ex.Message}");
            }
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred submitting the report", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/reports")]
    public async Task<IActionResult> GetRaceReports([FromRoute] long raceId)
    {
        try
        {
            var userId = GetCurrentUserId();
            // Validate if the referee is actually assigned to the race
            var _ = await _refereeService.GetHorseChecksAsync(userId, raceId);

            var response = await _refereeService.GetReportsByRaceIdAsync(raceId);
            if (response == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }
            return Ok(response);
        }
        catch (InvalidOperationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred retrieving race reports", detail = ex.Message });
        }
    }

    [HttpPost("races/{raceId}/results")]
    public async Task<IActionResult> SubmitResultRoute([FromRoute] long raceId, [FromBody] SubmitRaceResultRequest request)
    {
        try
        {
            request.RaceId = raceId;
            
            var response = await _resultService.SubmitResultAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred submitting the race result", detail = ex.Message });
        }
    }

    [HttpPost("results")]
    public async Task<IActionResult> SubmitResult([FromBody] SubmitRaceResultRequest request)
    {
        try
        {
            var response = await _resultService.SubmitResultAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred submitting the race result", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/results")]
    public async Task<IActionResult> GetRaceResults([FromRoute] long raceId)
    {
        try
        {
            var response = await _resultService.GetResultsByRaceIdAsync(raceId);
            if (response == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred retrieving race results", detail = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _refereeService.GetDashboardAsync(userId);
            return Ok(new { message = "Referee dashboard retrieved successfully", result = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving dashboard", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/horse-checks")]
    public async Task<IActionResult> GetHorseChecks(long raceId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var horseChecks = await _refereeService.GetHorseChecksAsync(userId, raceId);
            return Ok(new { message = "Horse checks retrieved successfully", result = horseChecks });
        }
        catch (InvalidOperationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving horse checks", detail = ex.Message });
        }
    }

    [HttpPut("violations/{id}")]
    public async Task<IActionResult> UpdateViolation(long id, [FromBody] UpdateViolationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var violation = await _refereeService.UpdateViolationAsync(userId, id, request);
            return Ok(new { message = "Violation updated successfully", result = violation });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not assigned"))
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred updating the violation", detail = ex.Message });
        }
    }
}
