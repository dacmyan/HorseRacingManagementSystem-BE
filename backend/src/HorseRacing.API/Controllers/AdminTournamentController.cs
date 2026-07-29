using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Infrastructure.Persistence;
using HorseRacing.Application.Features.Notifications.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using HorseRacing.Application.Features.TournamentAndRacing.Services;

namespace HorseRacing.API.Controllers
{
    [ApiController]
    [Route("api/admin/tournaments")]
    [Authorize(Roles = "Admin")]
    public class AdminTournamentController : ControllerBase
    {
        private readonly ITournamentService _tournamentService;

        public AdminTournamentController(ITournamentService tournamentService)
        {
            _tournamentService = tournamentService;
        }

        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendRegistration(long id)
        {
            if (id <= 0)
                return BadRequest(new { message = "Tournament ID must be greater than zero." });

            try
            {
                var response = await _tournamentService.ExtendRegistrationAsync(id);
                return Ok(response);
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
                return StatusCode(500, new { message = "An error occurred extending registration", detail = ex.Message });
            }
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelTournament(long id, [FromBody] CancelTournamentRequest request)
        {
            if (id <= 0)
                return BadRequest(new { message = "Tournament ID must be greater than zero." });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Reason for cancellation is required." });

            try
            {
                await _tournamentService.CancelTournamentAsync(id, request.Reason);
                return Ok(new { message = "Tournament cancelled successfully." });
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
                return StatusCode(500, new { message = "An error occurred cancelling the tournament", detail = ex.Message });
            }
        }
    }

    public sealed class CancelTournamentRequest
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }
}
