using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.Public.DTOs;
using HorseRacing.Application.Features.Public.Interfaces;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.DTOs;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Infrastructure.Repositories;

public class PublicQueryRepository : IPublicQueryRepository
{
    private readonly AppDbContext _context;
    private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");

    public PublicQueryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CheckDatabaseHealthAsync()
    {
        return await _context.Database.CanConnectAsync();
    }

    public async Task<List<JockeyRankingResponse>> GetJockeyRankingsAsync()
    {
        return await _context.JockeyProfiles
            .Include(jp => jp.User)
            .Where(jp => jp.Status == "Active")
            .OrderByDescending(jp => jp.RankingPoint)
            .Select(jp => new JockeyRankingResponse
            {
                JockeyId = (int)jp.JockeyId,
                UserId = jp.UserId,
                FullName = jp.User != null ? jp.User.FullName : "Unknown Jockey",
                Email = jp.User != null ? jp.User.Email : string.Empty,
                ExperienceYears = jp.ExperienceYears,
                RankingPoint = jp.RankingPoint
            })
            .ToListAsync();
    }

    public async Task<List<HorseRankingResponse>> GetHorseRankingsAsync()
    {
        var results = await _context.RaceResults.ToListAsync();
        
        var horses = await _context.Horses
            .Include(h => h.Owner)
            .ToListAsync();

        return horses
            .Select(h =>
            {
                var wins = results.Count(r => 
                    r.Winner.Equals(h.Name, StringComparison.OrdinalIgnoreCase) || 
                    r.Winner.Equals(h.HorseId.ToString()));

                return new HorseRankingResponse
                {
                    HorseId = (int)h.HorseId,
                    Name = h.Name,
                    Age = h.Age,
                    Breed = h.Breed,
                    OwnerName = h.Owner != null ? h.Owner.FullName : "Unknown Owner",
                    WinsCount = wins
                };
            })
            .OrderByDescending(h => h.WinsCount)
            .ToList();
    }

    public async Task<List<TournamentListResponseDto>> GetTournamentsAsync(bool isAdmin)
    {
        var query = _context.Tournaments.AsQueryable();

        if (!isAdmin)
        {
            var now = VietnamNow;
            query = query.Where(t => 
                (t.RegistrationStartDate.HasValue && t.RegistrationStartDate.Value <= now) || 
                (t.StartDate.HasValue && t.StartDate.Value <= now) ||
                (!t.RegistrationStartDate.HasValue && !t.StartDate.HasValue)
            );
        }

        var tournaments = await query.ToListAsync();

        var tournamentIds = tournaments.Select(t => t.TournamentId).ToList();
        
        var prizes = await _context.Prizes
            .Where(p => tournamentIds.Contains(p.TournamentId))
            .ToListAsync();

        var prizesGrouped = prizes.GroupBy(p => p.TournamentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var registrations = await _context.Registrations
            .Include(r => r.MedicalCheckRecords)
            .Where(r => tournamentIds.Contains(r.TournamentId))
            .ToListAsync();

        var registrationsGrouped = registrations.GroupBy(r => r.TournamentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return tournaments.Select(t => {
            var tournamentRegs = registrationsGrouped.ContainsKey(t.TournamentId)
                ? registrationsGrouped[t.TournamentId]
                : new List<Registration>();

            var approvedRegistration = tournamentRegs.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase));
            var qualifiedRegistration = tournamentRegs.Count(r => 
            {
                if (!string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase)) return false;
                var check = r.MedicalCheckRecords?.FirstOrDefault();
                if (check == null) return false;
                bool isMedicalPassed = string.Equals(check.MedicalResult, "Pass", StringComparison.OrdinalIgnoreCase) || 
                                       string.Equals(check.MedicalResult, "Passed", StringComparison.OrdinalIgnoreCase);
                bool isDopingNegative = !string.Equals(check.DopingResult, "Positive", StringComparison.OrdinalIgnoreCase);
                return isMedicalPassed && isDopingNegative;
            });

            return new TournamentListResponseDto {
                TournamentId = t.TournamentId,
                Name = t.Name,
                Description = t.Description,
                RegistrationStartDate = t.RegistrationStartDate,
                RegistrationEndDate = t.RegistrationEndDate,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                Status = t.Status,
                CancelCount = t.CancelCount,
                ApprovedRegistration = approvedRegistration,
                QualifiedRegistration = qualifiedRegistration,
                Prizes = prizesGrouped.ContainsKey(t.TournamentId)
                    ? prizesGrouped[t.TournamentId].Select(p => new TournamentPrizeDto { Id = p.Id, RankPosition = p.RankPosition, Amount = p.Amount }).ToList()
                    : new List<TournamentPrizeDto>()
            };
        }).ToList();
    }

    public async Task<TournamentDetailResponseDto?> GetTournamentDetailAsync(long tournamentId, bool isAdmin)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Rounds)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);
            
        if (tournament == null) return null;

        if (!isAdmin && 
            (!tournament.RegistrationStartDate.HasValue || tournament.RegistrationStartDate.Value > VietnamNow) && 
            (!tournament.StartDate.HasValue || tournament.StartDate.Value > VietnamNow))
        {
            return null;
        }

        var prizes = await _context.Prizes
            .Where(p => p.TournamentId == tournamentId)
            .Select(p => new TournamentPrizeDto { Id = p.Id, RankPosition = p.RankPosition, Amount = p.Amount })
            .ToListAsync();

        var registrations = await _context.Registrations
            .Include(r => r.MedicalCheckRecords)
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync();

        var approvedRegistration = registrations.Count(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase));
        var qualifiedRegistration = registrations.Count(r => 
        {
            if (!string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase)) return false;
            var check = r.MedicalCheckRecords?.FirstOrDefault();
            if (check == null) return false;
            bool isMedicalPassed = string.Equals(check.MedicalResult, "Pass", StringComparison.OrdinalIgnoreCase) || 
                                   string.Equals(check.MedicalResult, "Passed", StringComparison.OrdinalIgnoreCase);
            bool isDopingNegative = !string.Equals(check.DopingResult, "Positive", StringComparison.OrdinalIgnoreCase);
            return isMedicalPassed && isDopingNegative;
        });

        return new TournamentDetailResponseDto {
            TournamentId = tournament.TournamentId,
            Name = tournament.Name,
            Description = tournament.Description,
            RegistrationStartDate = tournament.RegistrationStartDate,
            RegistrationEndDate = tournament.RegistrationEndDate,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            Status = tournament.Status,
            CancelCount = tournament.CancelCount,
            ApprovedRegistration = approvedRegistration,
            QualifiedRegistration = qualifiedRegistration,
            Prizes = prizes,
            Rounds = tournament.Rounds?.Select(r => new RoundDto {
                RoundId = r.RoundId,
                TournamentId = r.TournamentId,
                Name = r.Name ?? "",
                Order = r.RoundNumber,
                Status = r.Status
            }).ToList() ?? new List<RoundDto>()
        };
    }

    public async Task<List<LiveRaceResponseDto>> GetLiveRacesAsync()
    {
        var liveStatuses = new[] { "Live", "Running", "InProgress", "Ongoing" };
        return await _context.Races
            .Include(r => r.Round)
                .ThenInclude(rd => rd.Tournament)
            .Where(r => liveStatuses.Contains(r.Status))
            .Select(r => new LiveRaceResponseDto
            {
                RaceId = r.RaceId,
                RaceName = r.Name,
                TournamentName = r.Round != null && r.Round.Tournament != null ? r.Round.Tournament.Name : "",
                StartTime = r.RaceDate,
                Status = r.Status
            })
            .ToListAsync();
    }

    public async Task<bool> IsTournamentVisibleAsync(long tournamentId, bool isAdmin)
    {
        if (isAdmin) return true;

        var tournament = await _context.Tournaments
            .Where(t => t.TournamentId == tournamentId)
            .Select(t => new { t.RegistrationStartDate, t.StartDate })
            .FirstOrDefaultAsync();

        if (tournament == null) return false;

        var now = VietnamNow;
        return (tournament.RegistrationStartDate.HasValue && tournament.RegistrationStartDate.Value <= now) || 
               (tournament.StartDate.HasValue && tournament.StartDate.Value <= now) ||
               (!tournament.RegistrationStartDate.HasValue && !tournament.StartDate.HasValue);
    }
}
