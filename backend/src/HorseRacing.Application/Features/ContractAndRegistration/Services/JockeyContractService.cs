using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.ContractAndRegistration.Services;

public class JockeyContractService : IJockeyContractService
{
    private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");

    private readonly IJockeyContractRepository _contractRepository;
    private readonly IHorseRepository _horseRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IEmailService _emailService;

    public JockeyContractService(
        IJockeyContractRepository contractRepository,
        IHorseRepository horseRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        ITournamentRepository tournamentRepository,
        IRegistrationRepository registrationRepository,
        IEmailService emailService)
    {
        _contractRepository = contractRepository;
        _horseRepository = horseRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _emailService = emailService;
    }

    private JockeyContractResponse MapToResponse(JockeyContract contract)
    {
        return new JockeyContractResponse
        {
            Id = contract.ContractId,
            HorseId = (int)contract.HorseId,
            HorseName = contract.Horse?.Name ?? "Unknown Horse",
            TournamentId = contract.TournamentId,
            TournamentName = contract.Tournament?.Name ?? "Unknown Tournament",
            OwnerId = contract.Horse?.OwnerId ?? 0,
            OwnerName = contract.Horse?.Owner?.FullName ?? "Unknown Owner",
            JockeyId = contract.JockeyId,
            JockeyName = contract.Jockey?.FullName ?? "Unknown Jockey",
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            Status = contract.Status,
            InvitationExpiredAt = contract.InvitationExpiredAt,
            CreatedAt = contract.CreatedAt
        };
    }

    public async Task<JockeyContractResponse> SendContractAsync(int ownerUserId, CreateJockeyContract request)
    {
        // 1. Verify horse exists and belongs to owner
        var horse = await _horseRepository.GetByIdAsync(request.HorseId);
        if (horse == null)
        {
            throw new ArgumentException($"Horse with ID {request.HorseId} not found.");
        }
        if (horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse.");
        }

        // Verify horse registration and medical check
        var registration = await _registrationRepository.GetByHorseIdAndTournamentIdAsync(request.HorseId, request.TournamentId);
        if (registration == null)
        {
            throw new InvalidOperationException("This horse is not registered in this tournament.");
        }
        var hasPassedMedical = registration.MedicalCheckRecords != null &&
                               registration.MedicalCheckRecords.Any(m => m.CheckType == "Initial" && m.MedicalResult == "Pass");
        if (!hasPassedMedical)
        {
            throw new InvalidOperationException("Cannot hire a jockey: The horse has not passed the medical examination for this tournament.");
        }

        // 2. Verify Jockey user exists and has Jockey role
        var jockeyUser = await _userRepository.GetByIdAsync(request.JockeyId);
        if (jockeyUser == null || jockeyUser.Role == null || !jockeyUser.Role.Name.Equals("Jockey", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The specified user is not a valid Jockey.");
        }
        if (!jockeyUser.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The specified Jockey is currently inactive.");
        }

        // Validate that the Jockey is not already contracted to another horse in this tournament
        var hasActiveContract = await _contractRepository.HasActiveContractForJockeyInTournamentAsync(request.JockeyId, request.TournamentId);
        if (hasActiveContract)
        {
            throw new InvalidOperationException("This jockey is already contracted to another horse in this tournament.");
        }

        if (request.InvitationExpiredAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("Expiration date must be in the future.");
        }

        var hasPendingOrActiveForHorse = await _contractRepository.HasPendingOrActiveContractForHorseInTournamentAsync(request.HorseId, request.TournamentId);
        if (hasPendingOrActiveForHorse)
        {
            throw new InvalidOperationException("This horse already has a pending or active contract in this tournament.");
        }

        // 3. Date validation
        if (request.StartDate >= request.EndDate)
        {
            throw new ArgumentException("Start date must be before end date.");
        }
        if (request.StartDate < DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Start date cannot be in the past.");
        }

        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId);
        if (tournament == null)
        {
            throw new ArgumentException($"Tournament with ID {request.TournamentId} not found.");
        }
        if ((!tournament.RegistrationStartDate.HasValue || tournament.RegistrationStartDate.Value > VietnamNow) &&
            (!tournament.StartDate.HasValue || tournament.StartDate.Value > VietnamNow))
        {
            throw new InvalidOperationException("Tournament has not started yet.");
        }
        if (!tournament.StartDate.HasValue || !tournament.EndDate.HasValue)
        {
            throw new InvalidOperationException("Tournament dates are not configured.");
        }

        if (tournament.RegistrationEndDate.HasValue)
        {
            var invitationExpiredAtVietnam = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(request.InvitationExpiredAt, "SE Asia Standard Time");
            if (invitationExpiredAtVietnam > tournament.RegistrationEndDate.Value)
            {
                throw new ArgumentException("Invitation expiration deadline cannot be set after the tournament's registration end date.");
            }
        }

        var contractStart = request.StartDate.Date;
        var contractEnd = request.EndDate.Date;
        var tournamentStart = tournament.StartDate.Value.Date;
        var tournamentEnd = tournament.EndDate.Value.Date;
        if (contractStart < tournamentStart || contractEnd > tournamentEnd)
        {
            throw new ArgumentException(
                $"Contract dates must be within the tournament period ({tournamentStart:yyyy-MM-dd} to {tournamentEnd:yyyy-MM-dd}).");
        }

        // 4. Create or reopen JockeyContract. The DB has a unique key on
        // TournamentId + HorseId + JockeyId, so cancelled/rejected invitations
        // are reused instead of inserting a duplicate row.
        var existingContract = await _contractRepository.GetByTournamentHorseAndJockeyAsync(
            request.TournamentId,
            request.HorseId,
            request.JockeyId);

        JockeyContract contract;
        if (existingContract != null)
        {
            if (existingContract.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A pending invitation already exists for this jockey, horse, and tournament.");
            }
            if (existingContract.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This jockey already has an active contract for this horse and tournament.");
            }

            existingContract.StartDate = request.StartDate;
            existingContract.EndDate = request.EndDate;
            existingContract.InvitationExpiredAt = request.InvitationExpiredAt;
            existingContract.Status = "Pending";
            existingContract.CreatedAt = DateTime.UtcNow;
            contract = existingContract;
        }
        else
        {
            contract = new JockeyContract
            {
                TournamentId = request.TournamentId,
                HorseId = request.HorseId,
                JockeyId = request.JockeyId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                InvitationExpiredAt = request.InvitationExpiredAt,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _contractRepository.AddAsync(contract);
        }

        await _contractRepository.SaveChangesAsync();

        // 5. Send notification to Jockey
        await _notificationService.SendNotificationToUserAsync(
            request.JockeyId,
            "New Contract Proposal",
            "You have received a riding invitation. Please respond before the expiration time.",
            "System",
            referenceId: (int)contract.ContractId,
            actionUrl: "/jockey/invitations"
        );

        // 6. Send emails to Owner and Jockey
        var ownerUser = await _userRepository.GetByIdAsync(ownerUserId);
        if (ownerUser != null && !string.IsNullOrEmpty(ownerUser.Email))
        {
            var ownerSubject = "Đề nghị hợp đồng đã được gửi";
            var ownerBody = $@"
                <h2>Đề nghị hợp đồng nài ngựa đã được gửi</h2>
                <p>Xin chào {ownerUser.FullName},</p>
                <p>Bạn đã gửi thành công lời mời thuê nài ngựa <strong>{jockeyUser.FullName}</strong> cho ngựa <strong>{horse.Name}</strong> tham gia giải đấu <strong>{tournament.Name}</strong>.</p>
                <p>Thời hạn hợp đồng: từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy}.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(ownerUser.Email, ownerSubject, ownerBody);
        }

        if (!string.IsNullOrEmpty(jockeyUser.Email))
        {
            var jockeySubject = "Lời mời hợp đồng nài ngựa mới";
            var jockeyBody = $@"
                <h2>Bạn có một lời mời hợp đồng mới!</h2>
                <p>Xin chào {jockeyUser.FullName},</p>
                <p>Bạn vừa nhận được lời mời cưỡi ngựa <strong>{horse.Name}</strong> từ chủ ngựa <strong>{horse.Owner?.FullName ?? "Owner"}</strong> trong giải đấu <strong>{tournament.Name}</strong>.</p>
                <p>Thời hạn hợp đồng: từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy}.</p>
                <p>Vui lòng đăng nhập hệ thống để phản hồi lời mời.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(jockeyUser.Email, jockeySubject, jockeyBody);
        }

        // Fetch populated contract for mapped response
        var populated = await _contractRepository.GetByIdAsync(contract.ContractId);
        return MapToResponse(populated ?? contract);
    }

    public async Task<IEnumerable<JockeyContractResponse>> GetContractsForJockeyAsync(int jockeyUserId)
    {
        var contracts = await _contractRepository.GetByJockeyIdAsync(jockeyUserId);
        await CheckAndUpdateExpiredContractsAsync(contracts);
        var now = VietnamNow;
        var filteredContracts = contracts.Where(c => c.Tournament == null || 
            (c.Tournament.RegistrationStartDate.HasValue && c.Tournament.RegistrationStartDate.Value <= now) || 
            (c.Tournament.StartDate.HasValue && c.Tournament.StartDate.Value <= now));
        return filteredContracts.Select(MapToResponse);
    }

    public async Task<IEnumerable<JockeyContractResponse>> GetContractsForOwnerAsync(int ownerUserId)
    {
        var contracts = await _contractRepository.GetByOwnerIdAsync(ownerUserId);
        await CheckAndUpdateExpiredContractsAsync(contracts);
        var now = VietnamNow;
        var filteredContracts = contracts.Where(c => c.Tournament == null || 
            (c.Tournament.RegistrationStartDate.HasValue && c.Tournament.RegistrationStartDate.Value <= now) || 
            (c.Tournament.StartDate.HasValue && c.Tournament.StartDate.Value <= now));
        return filteredContracts.Select(MapToResponse);
    }

    public async Task<JockeyContractResponse> RespondToContractAsync(int jockeyUserId, int contractId, RespondToContractRequest request)
    {
        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract == null)
        {
            throw new ArgumentException($"Jockey contract with ID {contractId} not found.");
        }

        if (contract.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) && DateTime.UtcNow > contract.InvitationExpiredAt)
        {
            contract.Status = "Expired";
            await _contractRepository.SaveChangesAsync();

            // Notify Owner
            await _notificationService.SendNotificationToUserAsync(
                contract.Horse?.OwnerId ?? 0,
                "Invitation Expired",
                "The invitation has expired. You can invite another jockey.",
                "System",
                referenceId: contract.ContractId,
                actionUrl: "/owner/jockeys"
            );
        }

        if (contract.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This invitation has expired.");
        }

        if (contract.Tournament != null && 
            (!contract.Tournament.RegistrationStartDate.HasValue || contract.Tournament.RegistrationStartDate.Value > VietnamNow) && 
            (!contract.Tournament.StartDate.HasValue || contract.Tournament.StartDate.Value > VietnamNow))
        {
            throw new ArgumentException($"Jockey contract with ID {contractId} not found.");
        }
        if (contract.JockeyId != jockeyUserId)
        {
            throw new InvalidOperationException("Access denied. You cannot respond to this contract.");
        }
        if (!contract.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Contract is already '{contract.Status}' and cannot be responded to.");
        }

        if (request.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase) || request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            var hasActiveContract = await _contractRepository.HasActiveContractForJockeyInTournamentAsync(jockeyUserId, contract.TournamentId);
            if (hasActiveContract)
            {
                throw new InvalidOperationException("You cannot ride multiple horses in the same tournament.");
            }
        }

        contract.Status = request.Status;

        // If accepting, cancel/expire other active contracts for the same horse and cancel other pending contracts for the same jockey in this tournament
        if (contract.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase) || contract.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            var existingContract = await _contractRepository.GetActiveContractForHorseAsync((int)contract.HorseId, contract.TournamentId);
            if (existingContract != null)
            {
                existingContract.Status = "Expired";
            }

            var otherPendingContracts = await _contractRepository.GetOtherPendingContractsForJockeyInTournamentAsync(
                jockeyUserId, 
                contract.TournamentId, 
                contract.ContractId
            );

            foreach (var otherContract in otherPendingContracts)
            {
                otherContract.Status = "Cancelled";
                try
                {
                    await _notificationService.SendNotificationToUserAsync(
                        otherContract.Horse?.OwnerId ?? 0,
                        "Lời mời nài ngựa bị hủy",
                        $"Lời mời nài ngựa cho ngựa '{otherContract.Horse?.Name ?? "Horse"}' gửi tới Jockey '{contract.Jockey?.FullName ?? "Jockey"}' đã bị tự động hủy do Jockey đã nhận lời mời từ chủ ngựa khác trong giải đấu '{contract.Tournament?.Name ?? "Tournament"}'.",
                        "System",
                        referenceId: (int)otherContract.ContractId,
                        actionUrl: "/owner/jockeys"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Notification Error] Failed to notify owner of cancelled contract: {ex.Message}");
                }
            }
        }

        await _contractRepository.SaveChangesAsync();

        // Notify Owner of response
        await _notificationService.SendNotificationToUserAsync(
            contract.Horse?.OwnerId ?? 0,
            "Contract Response",
            $"Jockey '{contract.Jockey?.FullName ?? "Jockey"}' responded '{request.Status}' to contract proposal ID {contract.ContractId} for horse '{contract.Horse?.Name ?? "Horse"}'.",
            "System",
            referenceId: (int)contract.ContractId,
            actionUrl: "/owner/jockeys"
        );

        // Send emails
        var ownerUser = await _userRepository.GetByIdAsync(contract.Horse?.OwnerId ?? 0);
        if (ownerUser != null && !string.IsNullOrEmpty(ownerUser.Email))
        {
            var ownerSubject = "Phản hồi hợp đồng nài ngựa";
            var ownerBody = $@"
                <h2>Phản hồi từ Nài ngựa</h2>
                <p>Xin chào {ownerUser.FullName},</p>
                <p>Nài ngựa <strong>{contract.Jockey?.FullName ?? "Jockey"}</strong> đã phản hồi <strong>{request.Status}</strong> đối với lời mời cưỡi ngựa <strong>{contract.Horse?.Name ?? "Horse"}</strong>.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(ownerUser.Email, ownerSubject, ownerBody);
        }

        var jockeyUser = await _userRepository.GetByIdAsync(jockeyUserId);
        if (jockeyUser != null && !string.IsNullOrEmpty(jockeyUser.Email))
        {
            var jockeySubject = "Cập nhật trạng thái hợp đồng nài ngựa";
            var jockeyBody = $@"
                <h2>Cập nhật Hợp đồng</h2>
                <p>Xin chào {jockeyUser.FullName},</p>
                <p>Bạn đã phản hồi <strong>{request.Status}</strong> cho hợp đồng cưỡi ngựa <strong>{contract.Horse?.Name ?? "Horse"}</strong>.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(jockeyUser.Email, jockeySubject, jockeyBody);
        }

        return MapToResponse(contract);
    }

    public async Task<JockeyContractResponse> CancelContractAsync(int ownerUserId, int contractId)
    {
        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract == null)
            throw new ArgumentException("Contract not found.");

        if (contract.Horse?.OwnerId != ownerUserId)
            throw new InvalidOperationException("You can only cancel contracts for your own horses.");

        if (contract.Status != "Pending")
            throw new InvalidOperationException($"Contract is already {contract.Status} and cannot be cancelled.");

        contract.Status = "Cancelled";
        await _contractRepository.SaveChangesAsync();

        await _notificationService.SendNotificationToUserAsync(
            contract.JockeyId,
            "Contract Proposal Cancelled",
            $"Contract proposal for horse '{contract.Horse?.Name ?? "Horse"}' has been cancelled by the Owner.",
            "System",
            referenceId: (int)contract.ContractId,
            actionUrl: "/jockey/invitations"
        );

        // Send Emails
        var ownerUser = await _userRepository.GetByIdAsync(ownerUserId);
        if (ownerUser != null && !string.IsNullOrEmpty(ownerUser.Email))
        {
            var ownerSubject = "Hủy lời mời nài ngựa";
            var ownerBody = $@"
                <h2>Hủy hợp đồng</h2>
                <p>Xin chào {ownerUser.FullName},</p>
                <p>Bạn đã hủy lời mời nài ngựa đối với hợp đồng của ngựa <strong>{contract.Horse?.Name ?? "Horse"}</strong>.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(ownerUser.Email, ownerSubject, ownerBody);
        }

        var jockeyUser = await _userRepository.GetByIdAsync(contract.JockeyId);
        if (jockeyUser != null && !string.IsNullOrEmpty(jockeyUser.Email))
        {
            var jockeySubject = "Hợp đồng bị hủy từ chủ ngựa";
            var jockeyBody = $@"
                <h2>Hủy hợp đồng</h2>
                <p>Xin chào {jockeyUser.FullName},</p>
                <p>Lời mời cưỡi ngựa <strong>{contract.Horse?.Name ?? "Horse"}</strong> đã bị hủy bởi chủ ngựa.</p>
                <p>Trân trọng,</p>
            ";
            await _emailService.SendEmailAsync(jockeyUser.Email, jockeySubject, jockeyBody);
        }

        return MapToResponse(contract);
    }

    public async Task<bool> CheckJockeyBusyAsync(int jockeyId, long tournamentId)
    {
        return await _contractRepository.HasActiveContractForJockeyAsync(jockeyId, tournamentId);
    }

    public async Task<List<int>> GetBusyJockeysForTournamentAsync(long tournamentId)
    {
        return await _contractRepository.GetBusyJockeysForTournamentAsync(tournamentId);
    }

    public async Task<bool> CheckHorseBusyAsync(int horseId, long tournamentId)
    {
        return await _contractRepository.HasActiveContractForHorseAsync(horseId, tournamentId);
    }

    private async Task CheckAndUpdateExpiredContractsAsync(IEnumerable<JockeyContract> contracts)
    {
        var pendingExpired = contracts
            .Where(c => c.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) 
                && DateTime.UtcNow > c.InvitationExpiredAt)
            .ToList();

        if (pendingExpired.Any())
        {
            foreach (var contract in pendingExpired)
            {
                contract.Status = "Expired";
                // Notify Owner
                await _notificationService.SendNotificationToUserAsync(
                    contract.Horse?.OwnerId ?? 0,
                    "Invitation Expired",
                    "The invitation has expired. You can invite another jockey.",
                    "System",
                    referenceId: contract.ContractId,
                    actionUrl: "/owner/jockeys"
                );
            }
            await _contractRepository.SaveChangesAsync();
        }
    }
}
