using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HorseRacing.API.Filters;
using System.Collections.Generic;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/jockeys")]
[Authorize(Roles = "Jockey")]
public class JockeyController : ControllerBase
{
    private readonly IJockeyContractService _jockeyContractService;
    private readonly IJockeyService _jockeyService;

    public JockeyController(IJockeyContractService jockeyContractService, IJockeyService jockeyService)
    {
        _jockeyContractService = jockeyContractService;
        _jockeyService = jockeyService;
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

    [HttpGet("contracts")]
    public async Task<IActionResult> GetMyContracts()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _jockeyContractService.GetContractsForJockeyAsync(userId);
            return Ok(new { message = "Your contract proposals retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving your contracts", detail = ex.Message });
        }
    }

    [HttpPut("contracts/{id}/respond")]
    [BlockLockedUser]
    public async Task<IActionResult> RespondToContract(int id, [FromBody] RespondToContractRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _jockeyContractService.RespondToContractAsync(userId, id, request);
            return Ok(new { message = $"Contract successfully updated to '{request.Status}'", result = response });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred responding to the contract", detail = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetJockeyStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _jockeyService.GetJockeyStatsAsync(userId);
            return Ok(new { message = "Jockey stats retrieved successfully", result = stats });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving jockey stats", detail = ex.Message });
        }
    }

    [HttpGet("violations")]
    public async Task<IActionResult> GetJockeyViolations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var violations = await _jockeyService.GetJockeyViolationsAsync(userId);
            return Ok(new { message = "Jockey violations retrieved successfully", result = violations });
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

    [HttpGet("assigned-horses")]
    public async Task<IActionResult> GetAssignedHorses()
    {
        try
        {
            var userId = GetCurrentUserId();
            var assignments = await _jockeyService.GetAssignedHorsesAsync(userId);
            return Ok(new { message = "Assigned horses retrieved successfully", result = assignments });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving assigned horses", detail = ex.Message });
        }
    }
}
