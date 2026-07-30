using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Application.Features.HorseManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.UserManagement.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using HorseRacing.API.Filters;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "HorseOwner")]
public class OwnerController : ControllerBase
{
    private readonly IHorseService _horseService;
    private readonly IHorseDocumentService _horseDocumentService;
    private readonly IJockeyContractService _jockeyContractService;
    private readonly IRegistrationService _registrationService;
    private readonly IWalletService _walletService;
    private readonly IOwnerDashboardService _ownerDashboardService;

    public OwnerController(
        IHorseService horseService,
        IHorseDocumentService horseDocumentService,
        IJockeyContractService jockeyContractService,
        IRegistrationService registrationService,
        IWalletService walletService,
        IOwnerDashboardService ownerDashboardService)
    {
        _horseService = horseService;
        _horseDocumentService = horseDocumentService;
        _jockeyContractService = jockeyContractService;
        _registrationService = registrationService;
        _walletService = walletService;
        _ownerDashboardService = ownerDashboardService;
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

    [HttpPost("horses")]
    [BlockLockedUser]
    public async Task<IActionResult> CreateHorse([FromBody] RegisterHorseRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _horseService.CreateHorseAsync(userId, request);
            return CreatedAtAction(nameof(GetHorseById), new { id = response.Id }, new { message = "Horse registered successfully", result = response });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during horse creation", detail = ex.Message });
        }
    }

    [HttpGet("horses/my-horses")]
    public async Task<IActionResult> GetMyHorses()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _horseService.GetHorsesByOwnerAsync(userId);
            return Ok(new { message = "Horses retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving your horses", detail = ex.Message });
        }
    }

    [HttpGet("horses/{id}")]
    public async Task<IActionResult> GetHorseById(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _horseService.GetHorseByIdAsync(id, userId);
            if (response == null)
            {
                return NotFound(new { message = $"Horse with ID {id} not found or access denied." });
            }
            return Ok(new { message = "Horse details retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving horse details", detail = ex.Message });
        }
    }

    [HttpPut("horses/{id}")]
    [BlockLockedUser]
    public async Task<IActionResult> UpdateHorse(int id, [FromBody] UpdateHorseRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _horseService.UpdateHorseAsync(id, userId, request);
            return Ok(new { message = "Horse updated successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred updating horse details", detail = ex.Message });
        }
    }

    [HttpDelete("horses/{id}")]
    [BlockLockedUser]
    public async Task<IActionResult> DeleteHorse(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _horseService.DeleteHorseAsync(id, userId);
            return Ok(new { message = "Horse deleted successfully" });
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
            return StatusCode(500, new { message = "An error occurred deleting the horse", detail = ex.Message });
        }
    }

    [HttpPost("horses/{id}/documents")]
    [BlockLockedUser]
    public async Task<IActionResult> UploadDocument(int id, [FromBody] UploadHorseDocumentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _horseDocumentService.AddDocumentAsync(userId, id, request);
            return Ok(new { message = "Document uploaded successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred uploading the document", detail = ex.Message });
        }
    }

    [HttpPost("jockey-contracts")]
    [BlockLockedUser]
    public async Task<IActionResult> CreateContract([FromBody] CreateJockeyContract request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _jockeyContractService.SendContractAsync(userId, request);
            return Ok(new { message = "Jockey contract proposed successfully", result = response });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred sending the contract", detail = ex.Message });
        }
    }

    [HttpGet("jockey-contracts/my-proposals")]
    public async Task<IActionResult> GetMyProposedContracts()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _jockeyContractService.GetContractsForOwnerAsync(userId);
            return Ok(new { message = "Proposed contracts retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving proposed contracts", detail = ex.Message });
        }
    }

    [HttpGet("jockeys/{jockeyId:int}/check-busy/{tournamentId:long}")]
    public async Task<IActionResult> CheckJockeyBusy(int jockeyId, long tournamentId)
    {
        try
        {
            var isBusy = await _jockeyContractService.CheckJockeyBusyAsync(jockeyId, tournamentId);
            return Ok(new { isBusy });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error checking jockey status", detail = ex.Message });
        }
    }

    [HttpGet("tournaments/{tournamentId:long}/busy-jockeys")]
    public async Task<IActionResult> GetBusyJockeysForTournament(long tournamentId)
    {
        try
        {
            var busyJockeyIds = await _jockeyContractService.GetBusyJockeysForTournamentAsync(tournamentId);
            return Ok(new { busyJockeyIds });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving busy jockeys", detail = ex.Message });
        }
    }


    [HttpGet("horses/{horseId:int}/check-busy/{tournamentId:long}")]
    public async Task<IActionResult> CheckHorseBusy(int horseId, long tournamentId)
    {
        try
        {
            var isBusy = await _jockeyContractService.CheckHorseBusyAsync(horseId, tournamentId);
            return Ok(new { isBusy });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error checking horse status", detail = ex.Message });
        }
    }

    [HttpDelete("jockey-contracts/{id:int}")]
    [BlockLockedUser]
    public async Task<IActionResult> CancelContract(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _jockeyContractService.CancelContractAsync(userId, id);
            return Ok(new { message = "Jockey contract invitation cancelled successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred cancelling the contract", detail = ex.Message });
        }
    }

    [HttpPost("registrations")]
    [BlockLockedUser]
    public async Task<IActionResult> RegisterHorse([FromBody] CreateRegistrationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _registrationService.RegisterHorseAsync(userId, request);
            return Ok(new { message = "Tournament registration submitted successfully", result = response });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred submitting registration", detail = ex.Message });
        }
    }

    [HttpGet("registrations/my-registrations")]
    public async Task<IActionResult> GetMyRegistrations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _registrationService.GetRegistrationsByOwnerAsync(userId);
            return Ok(new { message = "Your registrations retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving registrations", detail = ex.Message });
        }
    }

    [HttpDelete("registrations/{id:long}")]
    public async Task<IActionResult> CancelRegistration(long id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _registrationService.CancelRegistrationByOwnerAsync(userId, id);
            return Ok(new { message = "Registration cancelled successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred cancelling registration", detail = ex.Message });
        }
    }


    [HttpGet("owner/results")]
    public async Task<IActionResult> GetOwnerResults()
    {
        try
        {
            var userId = GetCurrentUserId();
            var results = await _ownerDashboardService.GetOwnerResultsAsync(userId);
            return Ok(new { message = "Results retrieved successfully", result = results });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving results", detail = ex.Message });
        }
    }

    [HttpGet("owner/dashboard")]
    public async Task<IActionResult> GetOwnerDashboard()
    {
        try
        {
            var userId = GetCurrentUserId();
            var dashboard = await _ownerDashboardService.GetOwnerDashboardAsync(userId);
            return Ok(new { message = "Owner dashboard retrieved successfully", result = dashboard });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving owner dashboard", detail = ex.Message });
        }
    }

    [HttpGet("owner/wallet/balance")]
    public async Task<IActionResult> GetWalletBalance()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _walletService.GetBalanceAsync(userId);
            return Ok(new { message = "Wallet balance retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving balance", detail = ex.Message });
        }
    }

    [HttpGet("owner/wallet/history")]
    public async Task<IActionResult> GetWalletHistory()
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _walletService.GetTransactionHistoryAsync(userId);
            return Ok(new { message = "Transaction history retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving history", detail = ex.Message });
        }
    }

    [HttpPost("owner/wallet/withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _walletService.WithdrawAsync(userId, request);
            return Ok(new { message = "Withdrawal successful", result = response });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during withdrawal", detail = ex.Message });
        }
    }
}
