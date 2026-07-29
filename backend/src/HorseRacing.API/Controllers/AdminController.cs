using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.DTOs;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.DTOs;
using HorseRacing.Application.Features.TournamentAndRacing.Services;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IPrizePayoutService _prizePayoutService;
    private readonly IBetPayoutService _betPayoutService;
    private readonly ITournamentService _tournamentService;
    private readonly IRaceService _raceService;
    private readonly IRefereeAssignmentService _refereeAssignmentService;
    private readonly IRaceResultService _resultService;
    private readonly IRegistrationService _registrationService;

    public AdminController(
        IAdminService adminService,
        IPrizePayoutService prizePayoutService,
        IBetPayoutService betPayoutService,
        ITournamentService tournamentService,
        IRaceService raceService,
        IRefereeAssignmentService refereeAssignmentService,
        IRaceResultService resultService,
        IRegistrationService registrationService)
    {
        _adminService = adminService;
        _prizePayoutService = prizePayoutService;
        _betPayoutService = betPayoutService;
        _tournamentService = tournamentService;
        _raceService = raceService;
        _refereeAssignmentService = refereeAssignmentService;
        _resultService = resultService;
        _registrationService = registrationService;
    }

    private int GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameIdentifier))
        {
            nameIdentifier = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        }
        return int.Parse(nameIdentifier ?? "0");
    }

    [HttpGet("test")]
    public IActionResult TestAdminAuthorization()
    {
        return Ok(new { message = "Admin authorization successful" });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _adminService.GetRolesAsync();
        return Ok(new
        {
            message = "Roles retrieved successfully",
            result = roles
        });
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequestDto request)
    {
        try
        {
            var response = await _adminService.CreateAccountAsync(request);
            return Ok(new
            {
                message = "Account created successfully",
                result = response
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during account creation", detail = ex.Message });
        }
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        try
        {
            var accounts = await _adminService.GetAccountsAsync();
            return Ok(new
            {
                message = "Accounts retrieved successfully",
                result = accounts
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during retrieving accounts", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{id}/generate-races")]
    public async Task<IActionResult> GenerateRacesForTournament(long id)
    {
        try
        {
            var races = await _tournamentService.GenerateRacesForTournamentAsync(id);
            return Ok(new { message = "Races generated successfully", result = races });
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
            return StatusCode(500, new { message = "An error occurred generating races", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{tournamentId}/generate-final")]
    public async Task<IActionResult> GenerateFinal(long tournamentId)
    {
        try
        {
            var race = await _tournamentService.GenerateFinalRaceAsync(tournamentId);
            return Ok(new { message = "Final race generated successfully", result = race });
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
            return StatusCode(500, new { message = "An error occurred generating final race", detail = ex.Message });
        }
    }

    [HttpPost("races/{raceId}/recalculate-odds")]
    public async Task<IActionResult> RecalculateOdds(long raceId, [FromServices] HorseRacing.Application.Features.BettingEngine.Interfaces.IBettingService bettingService)
    {
        try
        {
            await bettingService.RecalculateRaceOddsAsync(raceId);
            return Ok(new { message = "Odds recalculated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred recalculating odds", detail = ex.Message });
        }
    }

    [HttpPost("payouts/prizes")]
    public async Task<IActionResult> DistributeTournamentPrizes([FromBody] PrizePayoutRequest request)
    {
        try
        {
            request.TriggeredByUserId = GetCurrentUserId();
            await _prizePayoutService.ProcessPrizePayoutAsync(request);
            return Ok(new { message = "Tournament prizes distributed successfully" });
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
            return StatusCode(500, new { message = "An error occurred during tournament prize distribution", detail = ex.Message });
        }
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var payouts = await dashboardService.GetPayoutsAsync();
            return Ok(new { message = "Payouts retrieved successfully", result = payouts });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving payouts", detail = ex.Message });
        }
    }

    [HttpGet("wallet/balance")]
    public async Task<IActionResult> GetWalletBalance([FromServices] IWalletService walletService)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await walletService.GetBalanceAsync(userId);
            return Ok(new { message = "Admin wallet balance retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WALLET BALANCE ERROR]: {ex}");
            return StatusCode(500, new { message = "An error occurred retrieving admin wallet balance", detail = ex.Message });
        }
    }

    [HttpGet("wallet/history")]
    public async Task<IActionResult> GetWalletHistory([FromServices] IWalletService walletService)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await walletService.GetTransactionHistoryAsync(userId);
            return Ok(new { message = "Admin wallet history retrieved successfully", result = response });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WALLET HISTORY ERROR]: {ex}");
            return StatusCode(500, new { message = "An error occurred retrieving admin wallet history", detail = ex.Message });
        }
    }

    [HttpPost("wallet/deposit")]
    public async Task<IActionResult> DepositWallet([FromBody] DepositRequest request, [FromServices] IWalletService walletService)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await walletService.DepositAsync(userId, request);
            return Ok(new { message = "Treasury deposit successful", result = response });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WALLET DEPOSIT ERROR]: {ex}");
            return StatusCode(500, new { message = "An error occurred during treasury deposit", detail = ex.Message });
        }
    }

    [HttpPost("wallet/withdraw")]
    public async Task<IActionResult> WithdrawWallet([FromBody] WithdrawRequest request, [FromServices] IWalletService walletService)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await walletService.WithdrawAsync(userId, request);
            return Ok(new { message = "Treasury withdrawal successful", result = response });
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
            Console.WriteLine($"[WALLET WITHDRAW ERROR]: {ex}");
            return StatusCode(500, new { message = "An error occurred during treasury withdrawal", detail = ex.Message });
        }
    }

    [HttpPost("payouts/trigger/{raceId}")]
    public async Task<IActionResult> TriggerBetPayout(long raceId)
    {
        try
        {
            await _betPayoutService.ProcessPayoutAsync(raceId);
            return Ok(new { message = "Bet payouts processed successfully" });
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
            return StatusCode(500, new { message = "An error occurred processing bet payouts", detail = ex.Message });
        }
    }

    [HttpPost("tournaments")]
    
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        try
        {
            var adminUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminUserIdValue, out var adminUserId))
                return Unauthorized(new { message = "Unable to identify the Admin wallet." });

            var response = await _tournamentService.CreateTournamentAsync(request, adminUserId);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "An error occurred during tournament creation" });
        }
    }

    [HttpPut("tournaments/{id}")]
    public async Task<IActionResult> UpdateTournament([FromRoute] long id, [FromBody] UpdateTournamentRequest request)
    {
        try
        {
            var response = await _tournamentService.UpdateTournamentAsync(id, request);
            if (response == null)
            {
                return NotFound(new { message = $"Tournament with ID {id} was not found." });
            }
            return Ok(new { message = "Tournament updated successfully", result = response });
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during tournament update", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{id}/close-registration")]
    public async Task<IActionResult> CloseRegistration(long id)
    {
        try
        {
            var result = await _tournamentService.CloseRegistrationAsync(id, manualClose: true);
            return Ok(new { message = "Registration closed successfully.", result });
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
            return StatusCode(500, new { message = "An error occurred closing registration", detail = ex.Message });
        }
    }

    [HttpPost("races")]

    public async Task<IActionResult> CreateRace([FromBody] CreateRaceRequest request)
    {
        try
        {
            var response = await _raceService.CreateRaceAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "An error occurred during race scheduling" });
        }
    }

    [HttpPost("races/{raceId}/entries")]
    public async Task<IActionResult> CreateRaceEntry([FromRoute] long raceId, [FromBody] CreateRaceEntryRequest request)
    {
        try
        {
            var response = await _raceService.CreateRaceEntryAsync(raceId, request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            return StatusCode(500, new { message = "An error occurred during race entry creation", detail = ex.Message });
        }
    }

    [HttpDelete("races/{raceId}")]
    public async Task<IActionResult> DeleteRace([FromRoute] long raceId)
    {
        try
        {
            await _raceService.DeleteRaceAsync(raceId);
            return Ok(new { message = "Race deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            return StatusCode(500, new { message = "An error occurred during race deletion", detail = ex.Message });
        }
    }

    [HttpPut("races/{id}")]
    public async Task<IActionResult> UpdateRace([FromRoute] long id, [FromBody] UpdateRaceRequest request)
    {
        try
        {
            var response = await _raceService.UpdateRaceAsync(id, request);
            if (response == null)
            {
                return NotFound(new { message = $"Race with ID {id} not found." });
            }
            return Ok(new { message = "Race updated successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred during race update", detail = ex.Message });
        }
    }

    [HttpPost("races/{raceId}/referees")]
    public async Task<IActionResult> AssignReferee([FromRoute] long raceId, [FromBody] AssignRefereeRequest request)
    {
        try
        {
            var response = await _refereeAssignmentService.AssignRefereeAsync(raceId, request);
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
            return StatusCode(500, new { message = "An error occurred during referee assignment", detail = ex.Message });
        }
    }

    [HttpGet("races/{raceId}/referees")]
    public async Task<IActionResult> GetAssignedReferees([FromRoute] long raceId)
    {
        try
        {
            var response = await _refereeAssignmentService.GetAssignedRefereesAsync(raceId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving assigned referees", detail = ex.Message });
        }
    }

    [HttpDelete("races/{raceId}/referees/{refereeId}")]
    public async Task<IActionResult> RemoveRefereeAssignment([FromRoute] long raceId, [FromRoute] int refereeId)
    {
        try
        {
            await _refereeAssignmentService.RemoveRefereeAssignmentAsync(raceId, refereeId);
            return Ok(new { message = "Referee assignment removed successfully" });
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
            return StatusCode(500, new { message = "An error occurred removing referee assignment", detail = ex.Message });
        }
    }

    // TODO: Discuss refactoring with the team
    [HttpPost("races/{raceId}/publish")]
    public async Task<IActionResult> PublishResult([FromRoute] long raceId, [FromServices] AppDbContext context)
    {
        try
        {
            var race = await context.Races
                .AsNoTracking()
                .Include(r => r.Round)
                .FirstOrDefaultAsync(r => r.RaceId == raceId);

            if (race == null)
            {
                return NotFound(new { message = $"Race with ID {raceId} was not found." });
            }

            var response = await _resultService.PublishResultAsync(raceId);

            var isFinalRace = race.Round?.RoundNumber == 2;
            if (isFinalRace)
            {
                var tournamentId = race.Round!.TournamentId;
                var alreadyPaid = await context.TournamentPrizePayouts
                    .AnyAsync(p => p.TournamentId == tournamentId);

                if (!alreadyPaid)
                {
                    await _prizePayoutService.ProcessPrizePayoutAsync(new PrizePayoutRequest
                    {
                        TournamentId = (int)tournamentId,
                        FirstPlacePrize = 0m,
                        SecondPlacePrize = 0m,
                        ThirdPlacePrize = 0m,
                        TriggeredByUserId = GetCurrentUserId()
                    });
                }

                return Ok(new
                {
                    message = alreadyPaid
                        ? "Final race result published successfully. Tournament prizes had already been distributed."
                        : "Final race result published successfully. Prizes were deducted from the Admin wallet and credited to the Top 1, Top 2, and Top 3 horse owners.",
                    result = response
                });
            }

            return Ok(new { message = "Race result published successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred publishing the race result", detail = ex.Message });
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
            return StatusCode(500, new { message = "An error occurred retrieving race results", detail = ex.Message });
        }
    }

    [HttpGet("registrations")]
    public async Task<IActionResult> GetRegistrations([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var registrations = await dashboardService.GetRegistrationsAsync();
            return Ok(new { message = "Registrations retrieved successfully", result = registrations });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving registrations", detail = ex.Message });
        }
    }

    [HttpPut("registrations/{id}/status")]
    public async Task<IActionResult> ReviewRegistration(int id, [FromBody] ReviewRegistrationRequest request)
    {
        try
        {
            var validStatuses = new[] { "Approved", "Rejected" };
            request.Status = request.Status?.Trim() ?? string.Empty;
            if (!validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "Status must be 'Approved' or 'Rejected'." });
            request.Status = validStatuses.First(s => s.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
            var response = await _registrationService.ReviewRegistrationAsync(id, request);
            return Ok(new { message = $"Registration #{id} has been {request.Status.ToLower()}.", result = response });
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
            return StatusCode(500, new { message = "An error occurred reviewing registration", detail = ex.Message });
        }
    }

    [HttpGet("referees")]
    public async Task<IActionResult> GetReferees([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var referees = await dashboardService.GetRefereesAsync();
            return Ok(new { message = "Referees retrieved successfully", result = referees });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving referees", detail = ex.Message });
        }
    }

    [HttpGet("violations")]
    public async Task<IActionResult> GetViolations([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var violations = await dashboardService.GetViolationsAsync();
            return Ok(new { message = "Violations retrieved successfully", result = violations });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving violations", detail = ex.Message });
        }
    }

    [HttpGet("predictions/stats")]
    public async Task<IActionResult> GetPredictionStats([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var stats = await dashboardService.GetPredictionStatsAsync();
            return Ok(new { message = "Prediction stats retrieved successfully", result = stats });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving stats", detail = ex.Message });
        }
    }

    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var predictions = await dashboardService.GetPredictionsAsync();
            return Ok(new { message = "Predictions retrieved successfully", result = predictions });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving predictions", detail = ex.Message });
        }
    }

    [HttpGet("bets/stats")]
    public async Task<IActionResult> GetBetStats([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var stats = await dashboardService.GetBetStatsAsync();
            return Ok(new { message = "Bet stats retrieved successfully", result = stats });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving bet stats", detail = ex.Message });
        }
    }

    [HttpGet("bets")]
    public async Task<IActionResult> GetBets([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var bets = await dashboardService.GetBetsAsync();
            return Ok(new { message = "Bets retrieved successfully", result = bets });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving bets", detail = ex.Message });
        }
    }

    [HttpPut("registrations/{id}/approve")]
    public async Task<IActionResult> ApproveRegistration([FromRoute] long id)
    {
        try
        {
            var request = new ReviewRegistrationRequest { Status = "Approved" };
            var response = await _registrationService.ReviewRegistrationAsync(id, request);
            return Ok(new { message = "Registration approved successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred approving registration", detail = ex.Message });
        }
    }

    [HttpPut("registrations/{id}/reject")]
    public async Task<IActionResult> RejectRegistration([FromRoute] long id)
    {
        try
        {
            var request = new ReviewRegistrationRequest { Status = "Rejected" };
            var response = await _registrationService.ReviewRegistrationAsync(id, request);
            return Ok(new { message = "Registration rejected successfully", result = response });
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
            return StatusCode(500, new { message = "An error occurred rejecting registration", detail = ex.Message });
        }
    }

    [HttpGet("activity-log")]
    public async Task<IActionResult> GetActivityLog([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var sorted = await dashboardService.GetActivityLogAsync();
            return Ok(new { message = "Activity log retrieved successfully", result = sorted });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving activity log", detail = ex.Message });
        }
    }

    [HttpGet("referee-reports")]
    public async Task<IActionResult> GetRefereeReports([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var reports = await dashboardService.GetRefereeReportsAsync();
            return Ok(new { message = "Referee reports retrieved successfully", result = reports });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving referee reports", detail = ex.Message });
        }
    }

    [HttpGet("users/options")]
    public async Task<IActionResult> GetUserOptions([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var users = await dashboardService.GetUserOptionsAsync();
            return Ok(new { message = "User options retrieved successfully", result = users });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving user options", detail = ex.Message });
        }
    }

    [HttpGet("horses/options")]
    public async Task<IActionResult> GetHorseOptions([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var horses = await dashboardService.GetHorseOptionsAsync();
            return Ok(new { message = "Horse options retrieved successfully", result = horses });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving horse options", detail = ex.Message });
        }
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromQuery] bool forceLock = false)
    {
        try
        {
            var currentAdminId = GetCurrentUserId();
            var user = await _adminService.UpdateUserStatusAsync(id, currentAdminId, forceLock);
            return Ok(new { message = "User status updated successfully", result = user });
        }
        catch (LockConstraintException ex)
        {
            return BadRequest(new { message = ex.Message, blockers = ex.Blockers });
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
            return StatusCode(500, new { message = "An error occurred updating user status", detail = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var result = await dashboardService.GetDashboardStatsAsync();
            return Ok(new { message = "Dashboard stats retrieved successfully", result = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving dashboard stats", detail = ex.Message });
        }
    }

    [HttpPut("violations/{id}/status")]
    public async Task<IActionResult> UpdateViolationStatus(int id, [FromBody] UpdateViolationStatusRequest request, [FromServices] IAdminActionService adminActionService)
    {
        try
        {
            var result = await adminActionService.UpdateViolationStatusAsync(id, request.Status);
            return Ok(new { message = "Violation status updated successfully", result = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
            return StatusCode(500, new { message = "An error occurred updating violation status", detail = ex.Message });
        }
    }

    [HttpGet("races/referee-assignments")]
    public async Task<IActionResult> GetRacesRefereeAssignments([FromServices] IAdminDashboardService dashboardService)
    {
        try
        {
            var races = await dashboardService.GetRacesRefereeAssignmentsAsync();
            return Ok(new { message = "Races and referee assignments retrieved successfully", result = races });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving races and referee assignments", detail = ex.Message });
        }
    }

    [HttpPost("races/entries/{raceEntryId}/withdraw")]
    public async Task<IActionResult> WithdrawRaceEntry([FromRoute] long raceEntryId, [FromBody] WithdrawEntryRequest request, [FromServices] IAdminActionService adminActionService)
    {
        try
        {
            var result = await adminActionService.WithdrawRaceEntryAsync(raceEntryId, request.Reason);
            return Ok(new { 
                message = "Race entry has been successfully withdrawn/disqualified", 
                result = result 
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
            return StatusCode(500, new { message = "An error occurred during race entry withdrawal", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{tournamentId}/complete-racing")]
    public async Task<IActionResult> CompleteRacing(long tournamentId, [FromServices] ITournamentService tournamentService)
    {
        try
        {
            await tournamentService.CompleteRacingAsync(tournamentId);
            return Ok(new { message = "Tournament racing phase completed successfully, notifications sent." });
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
            return StatusCode(500, new { message = "An error occurred completing tournament racing phase", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{tournamentId}/complete")]
    public async Task<IActionResult> CompleteTournament(long tournamentId, [FromServices] ITournamentService tournamentService)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            await tournamentService.CompleteTournamentAsync(tournamentId, adminUserId);
            return Ok(new { message = "Tournament completed and prizes distributed successfully." });
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
            return StatusCode(500, new { message = "An error occurred during tournament completion", detail = ex.Message });
        }
    }

    [HttpPost("seed-su-tournaments")]
    public async Task<IActionResult> SeedSUTournaments([FromServices] HorseRacing.Infrastructure.Persistence.DataSeeder seeder)
    {
        try
        {
            await seeder.SeedSUTournamentsAsync();
            return Ok(new { message = "SU test tournaments seeded successfully (SU_48_HORSE: 50, SU_14_HORSE: 14, SU_11_HORSE: 11)." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error seeding SU tournaments", detail = ex.Message });
        }
    }
}


public class UpdateViolationStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class WithdrawEntryRequest
{
    public string? Reason { get; set; }
}
