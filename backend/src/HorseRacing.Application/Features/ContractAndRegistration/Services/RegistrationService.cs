using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.BettingEngine.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Application.Features.Notifications.Interfaces;

namespace HorseRacing.Application.Features.ContractAndRegistration.Services;

public class RegistrationService : IRegistrationService
{
    private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");

    private readonly IRegistrationRepository _registrationRepository;
    private readonly IHorseRepository _horseRepository;
    private readonly IBetRepository _betRepository;
    private readonly IJockeyContractRepository _contractRepository;
    private readonly INotificationService _notificationService;

    public RegistrationService(
        IRegistrationRepository registrationRepository,
        IHorseRepository horseRepository,
        IBetRepository betRepository,
        IJockeyContractRepository contractRepository,
        INotificationService notificationService)
    {
        _registrationRepository = registrationRepository;
        _horseRepository = horseRepository;
        _betRepository = betRepository;
        _contractRepository = contractRepository;
        _notificationService = notificationService;
    }

    private RegistrationResponse MapToResponse(Registration reg, JockeyContract? contract = null)
    {
        string? rejectionSource = null;
        if (reg.Status == "Rejected")
        {
            var hasFailedCheck = reg.MedicalCheckRecords != null && reg.MedicalCheckRecords.Any(m => m.MedicalResult == "Fail");
            rejectionSource = hasFailedCheck ? "Vet" : "Admin";
        }

        return new RegistrationResponse
        {
            RegistrationId = reg.RegistrationId,
            TournamentId = reg.TournamentId,
            TournamentName = reg.Tournament?.Name ?? "Unknown Tournament",
            HorseId = reg.HorseId,
            HorseName = reg.Horse?.Name ?? "Unknown Horse",
            Status = reg.Status,
            RegisteredAt = reg.RegisteredAt,
            JockeyId = contract?.JockeyId,
            JockeyName = contract?.Jockey?.FullName ?? contract?.Jockey?.Email,
            RejectionSource = rejectionSource
        };
    }

    public async Task<RegistrationResponse> RegisterHorseAsync(int ownerUserId, CreateRegistrationRequest request)
    {
        // 1. Verify Horse exists and belongs to owner
        var horse = await _horseRepository.GetByIdAsync(request.HorseId);
        if (horse == null)
        {
            throw new ArgumentException($"Horse with ID {request.HorseId} not found.");
        }
        if (horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse.");
        }

        // 2. Verify Tournament exists
        var tournament = await _betRepository.GetTournamentByIdAsync(request.TournamentId);
        if (tournament == null)
        {
            throw new ArgumentException($"Tournament with ID {request.TournamentId} not found.");
        }
        if ((!tournament.RegistrationStartDate.HasValue || tournament.RegistrationStartDate.Value > VietnamNow) &&
            (!tournament.StartDate.HasValue || tournament.StartDate.Value > VietnamNow))
        {
            throw new InvalidOperationException("Tournament has not started yet.");
        }

        var now = VietnamNow;
        if (tournament.RegistrationStartDate.HasValue && now < tournament.RegistrationStartDate.Value)
        {
            throw new InvalidOperationException($"Registration has not started yet. It opens on {tournament.RegistrationStartDate:yyyy-MM-dd HH:mm:ss} UTC.");
        }
        if (tournament.RegistrationEndDate.HasValue && now > tournament.RegistrationEndDate.Value)
        {
            throw new InvalidOperationException("Registration is closed.");
        }

        // 3. Verify owner maximum registered horses limit per tournament (Max 3 horses per owner)
        var ownerHorseCount = await _registrationRepository.CountRegistrationsByOwnerAndTournamentAsync(ownerUserId, request.TournamentId);
        if (ownerHorseCount >= 3)
        {
            throw new InvalidOperationException("You cannot register more than 3 horses for the same tournament.");
        }

        // 3.1. Verify horse is not already registered in this tournament
        var existing = await _registrationRepository.GetByHorseIdAndTournamentIdAsync(request.HorseId, request.TournamentId);
        if (existing != null)
        {
            if (existing.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                // Re-register: Update existing registration status back to PendingVet and update timestamp
                existing.Status = "PendingVet";
                existing.RegisteredAt = DateTime.UtcNow;
                
                await _registrationRepository.SaveChangesAsync();
                
                var populatedOld = await _registrationRepository.GetByIdAsync(existing.RegistrationId);
                return MapToResponse(populatedOld ?? existing);
            }

            throw new InvalidOperationException($"Horse '{horse.Name}' is already registered for this tournament.");
        }

        // 3.1. Verify horse HealthStatus
        if (horse.HealthStatus == "Injured" || horse.HealthStatus == "Recovering")
        {
            throw new InvalidOperationException($"Cannot register horse '{horse.Name}' because its health status is '{horse.HealthStatus}'.");
        }

        // 3.2. Verify overlapping dates
        var approvedRegistrations = await _registrationRepository.GetApprovedRegistrationsByHorseIdAsync(horse.HorseId);
        if (tournament.StartDate.HasValue && tournament.EndDate.HasValue)
        {
            foreach (var approvedReg in approvedRegistrations)
            {
                var approvedTournament = approvedReg.Tournament;
                if (approvedTournament != null && approvedTournament.StartDate.HasValue && approvedTournament.EndDate.HasValue)
                {
                    bool overlaps = tournament.StartDate <= approvedTournament.EndDate && tournament.EndDate >= approvedTournament.StartDate;
                    if (overlaps)
                    {
                        throw new InvalidOperationException($"Cannot register. The tournament dates ({tournament.StartDate:yyyy-MM-dd} to {tournament.EndDate:yyyy-MM-dd}) overlap with already approved registration for tournament '{approvedTournament.Name}' ({approvedTournament.StartDate:yyyy-MM-dd} to {approvedTournament.EndDate:yyyy-MM-dd}).");
                    }
                }
            }
        }

        // 4. Create Registration
        var registration = new Registration
        {
            TournamentId = request.TournamentId,
            HorseId = request.HorseId,
            Status = "PendingVet",
            RegisteredAt = DateTime.UtcNow
        };

        await _registrationRepository.AddAsync(registration);
        await _registrationRepository.SaveChangesAsync();

        var populated = await _registrationRepository.GetByIdAsync(registration.RegistrationId);

        if (populated != null)
        {
            try
            {
                var horseName = populated.Horse?.Name ?? "Horse";
                var tournamentName = populated.Tournament?.Name ?? "Tournament";
                await _notificationService.SendNotificationToRoleAsync(
                    "Veterinarian",
                    "New Medical Check Request",
                    $"Horse '{horseName}' has registered for tournament '{tournamentName}' and is awaiting medical inspection.",
                    "Medical",
                    referenceId: (int)populated.TournamentId,
                    actionUrl: "/vet/medical-check"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATION ERROR] Failed to send notification to Veterinarian: {ex.Message}");
            }
        }

        return MapToResponse(populated ?? registration);
    }

    public async Task<IEnumerable<RegistrationResponse>> GetRegistrationsByOwnerAsync(int ownerUserId)
    {
        var regs = await _registrationRepository.GetByOwnerIdAsync(ownerUserId);
        var now = VietnamNow;
        var filteredRegs = regs.Where(r => r.Tournament == null || 
            (r.Tournament.RegistrationStartDate.HasValue && r.Tournament.RegistrationStartDate.Value <= now) || 
            (r.Tournament.StartDate.HasValue && r.Tournament.StartDate.Value <= now)).ToList();

        // Fetch jockey contracts for this owner to enrich registrations with jockey names
        var contracts = await _contractRepository.GetByOwnerIdAsync(ownerUserId);
        var contractList = contracts.ToList();

        return filteredRegs.Select(reg =>
        {
            // Only bind jockeys that have accepted or active contracts
            var contract = contractList.FirstOrDefault(c =>
                c.HorseId == reg.HorseId &&
                c.TournamentId == reg.TournamentId &&
                (c.Status == "Accepted" || c.Status == "Active"));
            return MapToResponse(reg, contract);
        });
    }

    public async Task<RegistrationResponse> ReviewRegistrationAsync(long id, ReviewRegistrationRequest request)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
        {
            throw new KeyNotFoundException($"Registration with ID {id} not found.");
        }

        if (!registration.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Registration is already '{registration.Status}'. Only 'Pending' registrations can be reviewed.");
        }

        if (request.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (!await _registrationRepository.HasAcceptedJockeyContractAsync(registration.TournamentId, registration.HorseId))
                throw new InvalidOperationException("Cannot approve registration: An accepted or active jockey contract is required.");

            if (!await _registrationRepository.ApproveWithinCapacityAsync(id, registration.TournamentId, 48))
                throw new InvalidOperationException("Tournament capacity has been reached. A maximum of 48 horses can be approved; this registration remains pending as a waitlist entry.");

            registration.Status = "Approved";
        }
        else
        {
            registration.Status = request.Status;
            await _registrationRepository.SaveChangesAsync();
        }

        // Load fully populated registration for owner notifications
        var populated = await _registrationRepository.GetByIdAsync(id);
        var notifyReg = populated ?? registration;

        if (notifyReg.Horse != null)
        {
            var horseName = notifyReg.Horse.Name;
            var tournamentName = notifyReg.Tournament?.Name ?? "the tournament";
            var ownerId = notifyReg.Horse.OwnerId;

            if (request.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _notificationService.SendNotificationToUserAsync(
                        ownerId,
                        "Tournament Registration Approved",
                        $"Your horse '{horseName}' has been approved to participate in tournament '{tournamentName}'.",
                        "Tournament",
                        referenceId: (int)notifyReg.TournamentId,
                        actionUrl: "/owner/registrations"
                    );

                    // If the horse has an accepted/active jockey contract, notify the Jockey
                    var contracts = await _contractRepository.GetByOwnerIdAsync(ownerId);
                    var activeContract = contracts.FirstOrDefault(c =>
                        c.HorseId == notifyReg.HorseId &&
                        c.TournamentId == notifyReg.TournamentId &&
                        (c.Status == "Accepted" || c.Status == "Active"));

                    if (activeContract != null)
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            activeContract.JockeyId,
                            "Assigned Horse Approved",
                            $"Horse '{horseName}', which you are assigned to ride, has been approved for tournament '{tournamentName}'.",
                            "Tournament",
                            referenceId: (int)notifyReg.TournamentId,
                            actionUrl: "/jockey/schedule"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NOTIFICATION ERROR] Failed to send approval notification: {ex.Message}");
                }
            }
            else if (request.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _notificationService.SendNotificationToUserAsync(
                        ownerId,
                        "Tournament Registration Rejected",
                        $"The registration for horse '{horseName}' in tournament '{tournamentName}' was rejected. Please review the registration details.",
                        "Tournament",
                        referenceId: (int)notifyReg.TournamentId,
                        actionUrl: "/owner/registrations"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NOTIFICATION ERROR] Failed to send rejection notification to owner {ownerId}: {ex.Message}");
                }
            }
        }

        return MapToResponse(notifyReg);
    }

    public async Task CancelRegistrationByOwnerAsync(int ownerUserId, long registrationId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId);
        if (registration == null)
        {
            throw new KeyNotFoundException($"Registration with ID {registrationId} not found.");
        }
        if (registration.Horse == null || registration.Horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse registration.");
        }
        if (registration.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Registration is already cancelled.");
        }

        registration.Status = "Cancelled";
        await _registrationRepository.SaveChangesAsync();

        var horseName = registration.Horse?.Name ?? "Horse";
        var tournamentName = registration.Tournament?.Name ?? "Tournament";
        var ownerName = registration.Horse?.Owner?.FullName ?? registration.Horse?.Owner?.Email ?? "Horse Owner";

        try
        {
            await _notificationService.SendNotificationToRoleAsync(
                "Admin",
                "Tournament Registration Cancelled",
                $"Owner '{ownerName}' has cancelled the registration for horse '{horseName}' in tournament '{tournamentName}'.",
                "Registration",
                referenceId: (int)registration.TournamentId,
                actionUrl: "/admin/registrations"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NOTIFICATION ERROR] Failed to send cancellation notification to Admin: {ex.Message}");
        }
    }
}

