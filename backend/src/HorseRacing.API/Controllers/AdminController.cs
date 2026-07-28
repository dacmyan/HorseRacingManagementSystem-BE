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
using HorseRacing.Application.Features.Notifications.Interfaces;
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
    public async Task<IActionResult> GetPayouts([FromServices] AppDbContext context)
    {
        try
        {
            var payouts = await context.Payouts
                .Include(p => p.Bet)
                    .ThenInclude(b => b.User)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new {
                    PayoutId = p.Id,
                    BetId = p.BetId,
                    RaceId = p.Bet != null ? p.Bet.RaceId : 0,
                    SpectatorName = (p.Bet != null && p.Bet.User != null) ? p.Bet.User.FullName : "Unknown",
                    Amount = p.Amount,
                    Status = "Paid",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

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
    public async Task<IActionResult> GetRegistrations([FromServices] AppDbContext context)
    {
        try
        {
            var registrations = await context.Registrations
                .Include(r => r.Tournament)
                .Include(r => r.Horse)
                    .ThenInclude(h => h.Owner)
                .Select(r => new {
                    RegistrationId = r.RegistrationId,
                    TournamentId = r.TournamentId,
                    TournamentName = r.Tournament != null ? r.Tournament.Name : "",
                    HorseId = r.HorseId,
                    HorseName = r.Horse != null ? r.Horse.Name : "",
                    OwnerName = (r.Horse != null && r.Horse.Owner != null) ? r.Horse.Owner.FullName : "",
                    Status = r.Status,
                    HealthStatus = r.Horse != null ? r.Horse.HealthStatus : "Healthy",
                    RegisteredAt = r.RegisteredAt,
                    JockeyContractStatus = context.JockeyContracts
                        .Where(jc => jc.TournamentId == r.TournamentId && jc.HorseId == r.HorseId)
                        .OrderByDescending(jc => jc.CreatedAt)
                        .Select(jc => jc.Status)
                        .FirstOrDefault() ?? "NoContract",
                    JockeyName = context.JockeyContracts
                        .Where(jc => jc.TournamentId == r.TournamentId && jc.HorseId == r.HorseId)
                        .OrderByDescending(jc => jc.CreatedAt)
                        .Select(jc => jc.Jockey != null ? jc.Jockey.FullName : null)
                        .FirstOrDefault()
                })
                .ToListAsync();
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
    public async Task<IActionResult> GetReferees([FromServices] AppDbContext context)
    {
        try
        {
            var referees = await context.RefereeProfiles
                .Include(rp => rp.User)
                .Select(rp => new
                {
                    UserId = rp.UserId,
                    RefereeId = rp.RefereeId,
                    FullName = rp.User != null ? rp.User.FullName : "",
                    Email = rp.User != null ? rp.User.Email : "",
                    LicenseNumber = rp.LicenseNumber,
                    ExperienceYears = rp.ExperienceYears,
                    Status = string.IsNullOrWhiteSpace(rp.Status) ? (rp.User != null ? rp.User.Status : "Active") : rp.Status
                })
                .ToListAsync();
            return Ok(new { message = "Referees retrieved successfully", result = referees });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving referees", detail = ex.Message });
        }
    }

    [HttpGet("violations")]
    public async Task<IActionResult> GetViolations([FromServices] AppDbContext context)
    {
        try
        {
            var violations = await context.Violations
                .Include(v => v.Race)
                .Select(v => new {
                    ViolationId = v.Id,
                    RaceId = v.RaceId,
                    RaceName = v.Race != null ? v.Race.Name : "",
                    Type = v.Description.Contains(":") ? v.Description.Split(':', StringSplitOptions.None)[0] : "Violation",
                    Note = v.Description,
                    Penalty = v.Penalty,
                    Status = v.Status,
                    CreatedAt = DateTime.UtcNow
                })
                .ToListAsync();
            return Ok(new { message = "Violations retrieved successfully", result = violations });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving violations", detail = ex.Message });
        }
    }

    [HttpGet("predictions/stats")]
    public async Task<IActionResult> GetPredictionStats([FromServices] AppDbContext context)
    {
        try
        {
            var predictions = await context.Predictions.ToListAsync();
            var total = predictions.Count;
            var correct = predictions.Count(p => p.IsCorrect == true);
            var wrong = predictions.Count(p => p.IsCorrect == false);
            var accuracyRate = total > 0 ? (double)correct * 100 / total : 0;

            var stats = new {
                TotalPredictions = total,
                CorrectPredictions = correct,
                WrongPredictions = wrong,
                AccuracyRate = accuracyRate
            };

            return Ok(new { message = "Prediction stats retrieved successfully", result = stats });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving stats", detail = ex.Message });
        }
    }

    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions([FromServices] AppDbContext context)
    {
        try
        {
            var predictions = await context.Predictions
                .Include(p => p.User)
                .Include(p => p.Race)
                .Include(p => p.RaceEntry)
                    .ThenInclude(re => re.Registration)
                        .ThenInclude(reg => reg.Horse)
                .Select(p => new {
                    PredictionId = p.PredictionId,
                    SpectatorName = p.User != null ? p.User.FullName : "Unknown",
                    RaceName = p.Race != null ? p.Race.Name : "Unknown Race",
                    PredictedWinner = (p.RaceEntry != null && p.RaceEntry.Registration != null && p.RaceEntry.Registration.Horse != null) ? p.RaceEntry.Registration.Horse.Name : "Unknown Horse",
                    Point = p.Point,
                    IsCorrect = p.IsCorrect,
                    Status = p.Status,
                    PredictedAt = p.PredictedAt
                })
                .ToListAsync();

            return Ok(new { message = "Predictions retrieved successfully", result = predictions });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving predictions", detail = ex.Message });
        }
    }

    [HttpGet("bets/stats")]
    public async Task<IActionResult> GetBetStats([FromServices] AppDbContext context)
    {
        try
        {
            var bets = await context.Bets.ToListAsync();
            var totalBets = bets.Count;
            var totalAmount = bets.Sum(b => b.Amount);
            var wonBets = bets.Count(b => b.Status == "Won" || b.Status == "PaidOut");
            var pendingBets = bets.Count(b => b.Status == "Pending");
            var lostBets = bets.Count(b => b.Status == "Lost");

            var payouts = await context.Payouts.ToListAsync();
            var totalPayoutsPaid = payouts.Sum(p => p.Amount);
            var houseProfit = totalAmount - totalPayoutsPaid;

            var stats = new {
                TotalBets = totalBets,
                TotalAmount = totalAmount,
                WonBets = wonBets,
                PendingBets = pendingBets,
                LostBets = lostBets,
                TotalPayoutsPaid = totalPayoutsPaid,
                HouseProfit = houseProfit
            };

            return Ok(new { message = "Bet stats retrieved successfully", result = stats });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving bet stats", detail = ex.Message });
        }
    }

    [HttpGet("bets")]
    public async Task<IActionResult> GetBets([FromServices] AppDbContext context)
    {
        try
        {
            var bets = await context.Bets
                .Include(b => b.User)
                .Include(b => b.Race)
                .Include(b => b.Horse)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new {
                    BetId = b.Id,
                    SpectatorName = b.User != null ? b.User.FullName : "Unknown",
                    RaceName = b.Race != null ? b.Race.Name : "Unknown Race",
                    HorseName = b.Horse != null ? b.Horse.Name : "Unknown Horse",
                    Amount = b.Amount,
                    Odds = b.Odds,
                    PotentialPayout = b.Amount * b.Odds,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

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
    public async Task<IActionResult> GetActivityLog([FromServices] AppDbContext context)
    {
        try
        {
            var activities = new List<object>();

            // Recent users
            var recentUsers = await context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .Select(u => new { Type = "User", Title = "New user registered", Description = u.FullName + " (" + u.Email + ")", CreatedAt = u.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentUsers);

            // Recent registrations
            var recentRegistrations = await context.Registrations
                .Include(r => r.Horse)
                .Include(r => r.Tournament)
                .OrderByDescending(r => r.RegisteredAt)
                .Take(10)
                .Select(r => new { Type = "Registration", Title = "Horse registration " + r.Status, Description = (r.Horse != null ? r.Horse.Name : "") + " registered for " + (r.Tournament != null ? r.Tournament.Name : ""), CreatedAt = r.RegisteredAt })
                .ToListAsync();
            activities.AddRange(recentRegistrations);

            // Recent bets
            var recentBets = await context.Bets
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => new { Type = "Bet", Title = "Bet placed", Description = (b.User != null ? b.User.FullName : "Unknown") + " bet " + b.Amount + " on race " + b.RaceId, CreatedAt = b.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentBets);

            // Recent notifications
            var recentNotifications = await context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new { Type = "Notification", Title = "System notification", Description = n.Message, CreatedAt = n.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentNotifications);

            // Recent wallet transactions
            var recentTransactions = await context.Transactions
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new { Type = "Transaction", Title = "Wallet " + t.Type, Description = "Amount: " + t.Amount, CreatedAt = t.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentTransactions);

            // Sort all by CreatedAt descending and take top 50
            var sorted = activities
                .OrderByDescending(a => ((dynamic)a).CreatedAt)
                .Take(50)
                .ToList();

            return Ok(new { message = "Activity log retrieved successfully", result = sorted });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving activity log", detail = ex.Message });
        }
    }

    [HttpGet("referee-reports")]
    public async Task<IActionResult> GetRefereeReports([FromServices] AppDbContext context)
    {
        try
        {
            var reports = await context.RefereeReports
                .AsNoTracking()
                .OrderByDescending(report => report.CreatedAt)
                .Select(report => new
                {
                    reportId = report.ReportId,
                    assignmentId = report.AssignmentId,
                    raceId = report.Assignment != null ? report.Assignment.RaceId : 0,
                    raceName = report.Assignment != null && report.Assignment.Race != null
                        ? report.Assignment.Race.Name : string.Empty,
                    tournamentId = report.Assignment != null && report.Assignment.Race != null && report.Assignment.Race.Round != null
                        ? report.Assignment.Race.Round.TournamentId : 0,
                    tournamentName = report.Assignment != null && report.Assignment.Race != null && report.Assignment.Race.Round != null && report.Assignment.Race.Round.Tournament != null
                        ? report.Assignment.Race.Round.Tournament.Name : string.Empty,
                    refereeId = report.Assignment != null ? report.Assignment.RefereeId : 0,
                    refereeName = report.Assignment != null && report.Assignment.RefereeProfile != null && report.Assignment.RefereeProfile.User != null
                        ? report.Assignment.RefereeProfile.User.FullName : "Unknown Referee",
                    report.Content,
                    report.ViolationNote,
                    report.ReportedUserId,
                    reportedUserName = report.ReportedUser != null ? report.ReportedUser.FullName : null,
                    report.ReportedHorseId,
                    reportedHorseName = report.ReportedHorse != null ? report.ReportedHorse.Name : null,
                    report.CreatedAt
                })
                .ToListAsync();

            return Ok(new { message = "Referee reports retrieved successfully", result = reports });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving referee reports", detail = ex.Message });
        }
    }

    [HttpGet("users/options")]
    public async Task<IActionResult> GetUserOptions([FromServices] AppDbContext context)
    {
        try
        {
            var users = await context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == "Active")
                .Select(u => new
                {
                    Id = u.UserId,
                    Label = u.FullName,
                    Extra = u.Role != null ? u.Role.Name : "Unknown"
                })
                .ToListAsync();

            return Ok(new { message = "User options retrieved successfully", result = users });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving user options", detail = ex.Message });
        }
    }

    [HttpGet("horses/options")]
    public async Task<IActionResult> GetHorseOptions([FromServices] AppDbContext context)
    {
        try
        {
            var horses = await context.Horses
                .Include(h => h.Owner)
                .Select(h => new
                {
                    Id = (int)h.HorseId,
                    Label = h.Name,
                    Extra = "Owner: " + (h.Owner != null ? h.Owner.FullName : "Unknown")
                })
                .ToListAsync();

            return Ok(new { message = "Horse options retrieved successfully", result = horses });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving horse options", detail = ex.Message });
        }
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id)
    {
        try
        {
            var currentAdminId = GetCurrentUserId();
            var user = await _adminService.UpdateUserStatusAsync(id, currentAdminId);
            return Ok(new { message = "User status updated successfully", result = user });
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
    public async Task<IActionResult> GetDashboardStats([FromServices] AppDbContext context)
    {
        try
        {
            var totalUsers = await context.Users.CountAsync();
            var totalTournaments = await context.Tournaments.CountAsync();
            var activeRaces = await context.Races.CountAsync(r => r.Status == "Live" || r.Status == "Scheduled");
            var totalBets = await context.Bets.CountAsync();
            
            var totalRevenue = await context.Bets.Where(b => b.Status != "Pending").SumAsync(b => (decimal?)b.Amount) ?? 0;
            var totalPayout = await context.Payouts.SumAsync(p => (decimal?)p.Amount) ?? 0;

            var result = new
            {
                TotalUsers = totalUsers,
                TotalTournaments = totalTournaments,
                ActiveRaces = activeRaces,
                TotalBets = totalBets,
                TotalRevenue = totalRevenue,
                TotalPayout = totalPayout,
                Profit = totalRevenue - totalPayout
            };

            return Ok(new { message = "Dashboard stats retrieved successfully", result = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving dashboard stats", detail = ex.Message });
        }
    }

    [HttpPut("violations/{id}/status")]
    public async Task<IActionResult> UpdateViolationStatus(int id, [FromBody] UpdateViolationStatusRequest request, [FromServices] AppDbContext context, [FromServices] INotificationService notificationService)
    {
        try
        {
            var violation = await context.Violations.FindAsync(id);
            if (violation == null)
            {
                return NotFound(new { message = $"Violation with ID {id} was not found." });
            }

            var requestedStatus = request.Status?.Trim();
            var validViolationStatuses = new[] { "Pending", "Confirmed", "Rejected" };
            if (requestedStatus == null || !validViolationStatuses.Contains(requestedStatus, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid status. Must be 'Pending', 'Confirmed', or 'Rejected'." });
            }

            requestedStatus = validViolationStatuses.First(s => s.Equals(requestedStatus, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(violation.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(violation.Status, requestedStatus, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = $"A {violation.Status} violation cannot be changed to {requestedStatus}." });

            violation.Status = requestedStatus;
            await context.SaveChangesAsync();

            var refereeUserIds = await context.RaceRefereeAssignments
                .Where(a => a.RaceId == violation.RaceId && a.RefereeProfile != null)
                .Select(a => a.RefereeProfile!.UserId)
                .Distinct()
                .ToListAsync();
            foreach (var userId in refereeUserIds)
                await notificationService.SendNotificationToUserAsync(
                    userId,
                    "Violation report reviewed",
                    $"Violation #{violation.Id} for race #{violation.RaceId} has been {violation.Status.ToLowerInvariant()} by Admin.",
                    "Race",
                    referenceId: (int)violation.RaceId,
                    actionUrl: "/referee/violations");

            return Ok(new { message = "Violation status updated successfully", result = violation });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred updating violation status", detail = ex.Message });
        }
    }

    [HttpGet("races/referee-assignments")]
    public async Task<IActionResult> GetRacesRefereeAssignments([FromServices] AppDbContext context)
    {
        try
        {
            var races = await context.Races
                .Include(r => r.Round)
                    .ThenInclude(rd => rd.Tournament)
                .Include(r => r.RaceRefereeAssignments)
                    .ThenInclude(ra => ra.RefereeProfile)
                        .ThenInclude(rp => rp.User)
                .Select(r => new
                {
                    RaceId = r.RaceId,
                    RaceName = r.Name,
                    RaceDate = r.RaceDate,
                    Status = r.Status,
                    DistanceMeter = r.DistanceMeter,
                    RoundName = r.Round != null ? r.Round.Name : "",
                    TournamentName = (r.Round != null && r.Round.Tournament != null) ? r.Round.Tournament.Name : "",
                    Referees = r.RaceRefereeAssignments.Select(ra => new
                    {
                        RefereeId = ra.RefereeId,
                        FullName = (ra.RefereeProfile != null && ra.RefereeProfile.User != null) ? ra.RefereeProfile.User.FullName : "",
                        LicenseNumber = ra.RefereeProfile != null ? ra.RefereeProfile.LicenseNumber : "",
                        Status = ra.Status
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new { message = "Races and referee assignments retrieved successfully", result = races });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred retrieving races and referee assignments", detail = ex.Message });
        }
    }

    [HttpPost("races/entries/{raceEntryId}/withdraw")]
    public async Task<IActionResult> WithdrawRaceEntry([FromRoute] long raceEntryId, [FromBody] WithdrawEntryRequest request, [FromServices] AppDbContext context, [FromServices] INotificationService notificationService)
    {
        try
        {
            var entry = await context.RaceEntries
                .Include(re => re.Registration)
                    .ThenInclude(reg => reg.Horse)
                .Include(re => re.Race)
                .FirstOrDefaultAsync(re => re.RaceEntryId == raceEntryId);

            if (entry == null)
            {
                return NotFound(new { message = $"RaceEntry with ID {raceEntryId} was not found." });
            }

            var race = entry.Race;
            if (race == null)
            {
                return BadRequest(new { message = "Race entry is not associated with a valid race." });
            }

            var alreadyFinalStatuses = new[] { "Withdrawn", "Scratch", "DNF", "Disqualified", "Finished", "Completed" };
            if (alreadyFinalStatuses.Any(s => string.Equals(entry.Status, s, StringComparison.OrdinalIgnoreCase)))
            {
                var horseName = entry.Registration?.Horse?.Name ?? "This horse";
                if (string.Equals(entry.Status, "Withdrawn", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = $"Horse '{horseName}' has medical/health issues and has been automatically withdrawn from the race." });
                }
                return BadRequest(new { message = $"Race entry for horse '{horseName}' is already in final status '{entry.Status}'." });
            }

            if (string.Equals(race.Status, "Finished", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(race.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Cannot withdraw an entry from a finished/completed race." });
            }

            var isSickOrInjured = entry.Registration?.Horse != null && 
                (string.Equals(entry.Registration.Horse.HealthStatus, "Sick", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(entry.Registration.Horse.HealthStatus, "Injured", StringComparison.OrdinalIgnoreCase));

            if (!isSickOrInjured)
            {
                return BadRequest(new { message = "Cannot disqualify this horse. Only horses diagnosed by the veterinarian as Sick or Injured can be disqualified." });
            }

            var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "AdminDecision" : request.Reason.Trim();
            if (reason.Length > 500)
                return BadRequest(new { message = "Withdrawal reason cannot exceed 500 characters." });

            // Set status
            if (string.Equals(race.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = "DNF";
            }
            else
            {
                entry.Status = "Withdrawn";
            }

            entry.WithdrawReason = reason;
            entry.WithdrawTime = DateTime.UtcNow;

            if (entry.Registration != null)
            {
                entry.Registration.Status = "Disqualified";
            }

            await context.SaveChangesAsync();

            if (entry.Registration?.Horse != null)
            {
                var horse = entry.Registration.Horse;
                var notice = $"Horse '{horse.Name}' has been {entry.Status.ToLowerInvariant()} from race '{race.Name}'. Reason: {reason}.";
                await notificationService.SendNotificationToUserAsync(
                    horse.OwnerId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                    actionUrl: "/owner/registrations");

                var jockeyUserIds = await context.JockeyContracts
                    .Where(c => c.TournamentId == entry.Registration.TournamentId && c.HorseId == entry.Registration.HorseId &&
                                (c.Status == "Accepted" || c.Status == "Active"))
                    .Select(c => c.JockeyId)
                    .Distinct()
                    .ToListAsync();
                foreach (var userId in jockeyUserIds)
                    await notificationService.SendNotificationToUserAsync(
                        userId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                        actionUrl: "/jockey/schedule");

                var refereeUserIds = await context.RaceRefereeAssignments
                    .Where(a => a.RaceId == race.RaceId && a.RefereeProfile != null)
                    .Select(a => a.RefereeProfile!.UserId)
                    .Distinct()
                    .ToListAsync();
                foreach (var userId in refereeUserIds)
                    await notificationService.SendNotificationToUserAsync(
                        userId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                        actionUrl: "/referee/schedule");
            }

            return Ok(new { 
                message = "Race entry has been successfully withdrawn/disqualified", 
                result = new { 
                    raceEntryId = entry.RaceEntryId, 
                    status = entry.Status, 
                    healthStatus = entry.Registration?.Horse?.HealthStatus 
                } 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred during race entry withdrawal", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{tournamentId}/complete-racing")]
    public async Task<IActionResult> CompleteRacing(long tournamentId, [FromServices] AppDbContext context, [FromServices] INotificationService notificationService)
    {
        try
        {
            var tournament = await context.Tournaments
                .Include(t => t.Rounds)
                    .ThenInclude(r => r.Races)
                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);

            if (tournament == null)
            {
                return NotFound(new { message = $"Tournament with ID {tournamentId} not found." });
            }

            if (tournament.Status != "Active")
            {
                return BadRequest(new { message = $"Tournament is not in Active status. Current status: {tournament.Status}." });
            }
            var allRaces = tournament.Rounds.SelectMany(r => r.Races).ToList();
            if (allRaces.Count == 0 || allRaces.Any(r => !new[] { "Finished", "Completed" }.Contains(r.Status, StringComparer.OrdinalIgnoreCase)))
                return BadRequest(new { message = "All tournament races must be finished before completing the racing phase." });
            var finalRoundForValidation = tournament.Rounds.FirstOrDefault(r => r.RoundNumber == 2);
            if (finalRoundForValidation == null || finalRoundForValidation.Races.Count != 1)
                return BadRequest(new { message = "Tournament must have exactly one final race." });

            // 1. Update tournament status
            tournament.Status = "AwaitingResults";
            await context.SaveChangesAsync();

            // 2. Notify all active users
            await notificationService.BroadcastNotificationAsync(
                "Tournament Racing Completed",
                $"Racing for tournament '{tournament.Name}' has ended. The organizers are compiling the official results.",
                "Tournament",
                referenceId: (int)tournament.TournamentId,
                actionUrl: $"/spectator/tournaments/{tournament.TournamentId}"
            );

            // 3. Notify final race referee(s)
            var finalRound = tournament.Rounds.FirstOrDefault(r => r.RoundNumber == 2);
            if (finalRound != null)
            {
                var finalRace = finalRound.Races.FirstOrDefault();
                if (finalRace != null)
                {
                    var referees = await context.RaceRefereeAssignments
                        .Include(a => a.RefereeProfile)
                        .Where(a => a.RaceId == finalRace.RaceId)
                        .ToListAsync();

                    foreach (var assignment in referees)
                    {
                        if (assignment.RefereeProfile != null)
                        {
                            await notificationService.SendNotificationToUserAsync(
                                assignment.RefereeProfile.UserId,
                                "Tournament Results Submission Required",
                                $"Tournament '{tournament.Name}' has ended. Please submit all violation reports and record the horse rankings for Admin review.",
                                "System",
                                referenceId: (int)tournament.TournamentId,
                                actionUrl: "/referee/confirm-results"
                            );
                        }
                    }
                }
            }

            return Ok(new { message = "Tournament racing phase completed successfully, notifications sent." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred completing tournament racing phase", detail = ex.Message });
        }
    }

    [HttpPost("tournaments/{tournamentId}/complete")]
    public async Task<IActionResult> CompleteTournament(long tournamentId, [FromServices] AppDbContext context, [FromServices] INotificationService notificationService)
    {
        try
        {
            var tournament = await context.Tournaments
                .Include(t => t.Rounds)
                    .ThenInclude(r => r.Races)
                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);

            if (tournament == null)
            {
                return NotFound(new { message = $"Tournament with ID {tournamentId} not found." });
            }

            if (tournament.Status != "AwaitingResults")
            {
                return BadRequest(new { message = $"Tournament is in status '{tournament.Status}' and cannot be completed." });
            }

            // Verify final race is Finished
            var finalRound = tournament.Rounds.FirstOrDefault(r => r.RoundNumber == 2);
            if (finalRound == null)
            {
                return BadRequest(new { message = "Final round not found for this tournament." });
            }

            var finalRace = finalRound.Races.FirstOrDefault();
            if (finalRace == null)
            {
                return BadRequest(new { message = "Final race not found for this tournament." });
            }

            if (!string.Equals(finalRace.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Final race results must be published before completing the tournament. Current final race status: {finalRace.Status}." });
            }

            // Load configured prizes
            var configuredPrizes = await context.Prizes
                .Where(p => p.TournamentId == tournamentId)
                .ToListAsync();

            var p1 = configuredPrizes.FirstOrDefault(p => p.RankPosition == 1)?.Amount ?? 0m;
            var p2 = configuredPrizes.FirstOrDefault(p => p.RankPosition == 2)?.Amount ?? 0m;
            var p3 = configuredPrizes.FirstOrDefault(p => p.RankPosition == 3)?.Amount ?? 0m;

            if (p1 <= 0 || p2 <= 0 || p3 <= 0)
            {
                return BadRequest(new { message = "Tournament prize structure has not been configured yet. Please configure 1st, 2nd, and 3rd place prizes first." });
            }

            var request = new PrizePayoutRequest
            {
                TournamentId = (int)tournamentId,
                FirstPlacePrize = p1,
                SecondPlacePrize = p2,
                ThirdPlacePrize = p3,
                TriggeredByUserId = GetCurrentUserId()
            };

            await _prizePayoutService.ProcessPrizePayoutAsync(request);

            // Broadcast to all active users
            await notificationService.BroadcastNotificationAsync(
                "Tournament Completed and Prizes Awarded",
                $"Tournament '{tournament.Name}' has been completed successfully. Prize money has been awarded to the winning horse owners.",
                "Tournament",
                referenceId: (int)tournament.TournamentId,
                actionUrl: $"/spectator/tournaments/{tournament.TournamentId}"
            );

            return Ok(new { message = "Tournament completed and prizes distributed successfully." });
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
