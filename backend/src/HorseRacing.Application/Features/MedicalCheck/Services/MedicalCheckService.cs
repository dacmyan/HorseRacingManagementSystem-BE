using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Application.Features.MedicalCheck.DTOs;
using HorseRacing.Application.Features.MedicalCheck.Interfaces;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.MedicalCheck.Services;

public class MedicalCheckService : IMedicalCheckService
{
    private readonly IMedicalCheckRepository _repository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public MedicalCheckService(
        IMedicalCheckRepository repository,
        IRegistrationRepository registrationRepository,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _repository = repository;
        _registrationRepository = registrationRepository;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    // ─── Mapping ────────────────────────────────────────────────────────────
    private static MedicalCheckResponse Map(MedicalCheckRecord r) => new()
    {
        Id              = r.Id,
        RegistrationId  = r.RegistrationId,
        HorseId         = r.HorseId ?? r.Registration?.HorseId,
        HorseName       = r.Horse?.Name ?? r.Registration?.Horse?.Name,
        TournamentName  = r.Registration?.Tournament?.Name ?? "Normal Health Check",
        UserId          = r.UserId,
        CheckedByName   = r.Veterinarian != null ? (r.Veterinarian.FullName ?? r.Veterinarian.Username) : null,
        CheckType       = r.CheckType,
        Weight          = r.Weight,
        Temperature     = r.Temperature,
        HeartRate       = r.HeartRate,
        DopingResult    = r.DopingResult,
        MedicalResult   = r.MedicalResult,
        FailReason      = r.FailReason,
        Notes           = r.Notes,
        CheckedAt       = r.CheckedAt,
    };

    // ─── Queries ─────────────────────────────────────────────────────────────
    public async Task<IEnumerable<MedicalCheckResponse>> GetAllAsync()
    {
        var records = await _repository.GetAllAsync();
        return records.Select(Map);
    }

    public async Task<MedicalCheckResponse?> GetByIdAsync(long id)
    {
        var record = await _repository.GetByIdAsync(id);
        return record is null ? null : Map(record);
    }

    public async Task<IEnumerable<MedicalCheckResponse>> GetByRegistrationIdAsync(long registrationId)
    {
        var records = await _repository.GetByRegistrationIdAsync(registrationId);
        return records.Select(Map);
    }

    // ─── Commands ────────────────────────────────────────────────────────────
    // ─── Health Threshold Validation (Business Rule) ─────────────────────────
    /// <summary>
    /// A horse can only receive a "Pass" result if ALL of the following vital signs are within safe range:
    /// - Temperature: 37.2 – 38.3 °C (inclusive)
    /// - HeartRate:   28 – 44 bpm (inclusive)
    /// - DopingResult: must be "Negative"
    /// This rule is enforced at both the service layer (server-side) and the frontend (client-side).
    /// </summary>
    private static void ValidatePassEligibility(decimal? temperature, int? heartRate, decimal? weight, string dopingResult)
    {
        var errors = new List<string>();

        if (!temperature.HasValue || temperature.Value < 37.2m || temperature.Value > 38.3m)
            errors.Add("Temperature must be between 37.2 and 38.3 °C.");

        if (!heartRate.HasValue || heartRate.Value < 28 || heartRate.Value > 44)
            errors.Add("Heart Rate must be between 28 and 44 bpm.");

        if (!weight.HasValue || weight.Value < 300m || weight.Value > 700m)
            errors.Add("Weight must be between 300 and 700 kg.");

        if (!string.Equals(dopingResult, "Negative", StringComparison.OrdinalIgnoreCase))
            errors.Add("Doping Result must be Negative.");

        if (errors.Any())
        {
            throw new ArgumentException("Setting PASS status is not allowed due to the following reasons: " + string.Join(" ", errors));
        }
    }

    public async Task<MedicalCheckResponse> CreateAsync(int performedByUserId, CreateMedicalCheckRequest request)
    {
        if (request.Weight <= 0)
            throw new ArgumentException("Weight must be greater than zero.");

        var validDoping    = new[] { "Negative", "Positive" };
        var validMedical   = new[] { "Pass", "Fail" };

        if (!validDoping.Contains(request.DopingResult))
            throw new ArgumentException("DopingResult must be 'Negative' or 'Positive'.");

        if (!validMedical.Contains(request.MedicalResult))
            throw new ArgumentException("MedicalResult must be 'Pass' or 'Fail'.");

        if (request.MedicalResult == "Fail" && string.IsNullOrWhiteSpace(request.FailReason))
            throw new ArgumentException("FailReason is required when MedicalResult is 'Fail'.");

        // ✅ Business Rule: Validate vital signs thresholds before allowing Pass
        if (string.Equals(request.MedicalResult, "Pass", StringComparison.OrdinalIgnoreCase))
            ValidatePassEligibility(request.Temperature, request.HeartRate, request.Weight, request.DopingResult);

        // --- PATH A: Normal / General Health Check (MedicalRecordId is provided or RegistrationId is empty) ---
        if (request.MedicalRecordId.HasValue || !request.RegistrationId.HasValue || request.RegistrationId.Value == 0)
        {
            MedicalCheckRecord record = null;
            if (request.MedicalRecordId.HasValue)
            {
                record = await _repository.GetByIdAsync(request.MedicalRecordId.Value);
            }
            else if (request.HorseId.HasValue)
            {
                record = await _repository.GetPendingGeneralCheckByHorseIdAsync(request.HorseId.Value);
            }

            if (record == null)
            {
                if (request.HorseId.HasValue)
                {
                    record = new MedicalCheckRecord
                    {
                        HorseId = request.HorseId.Value,
                        RegistrationId = null,
                        CheckType = "General",
                        MedicalResult = "Pending",
                        Weight = 0,
                        CheckedAt = DateTime.UtcNow
                    };
                    await _repository.AddAsync(record);
                }
                else
                {
                    throw new ArgumentException("MedicalRecordId or HorseId is required for general health check.");
                }
            }

            var horse = await _repository.GetHorseByIdAsync(record.HorseId ?? request.HorseId ?? 0);
            if (horse == null)
                throw new ArgumentException("Associated horse not found.");

            // Update record values with actual check info
            record.UserId = performedByUserId;
            record.CheckType = "General";
            record.Weight = request.Weight;
            record.Temperature = request.Temperature;
            record.HeartRate = request.HeartRate;
            record.DopingResult = request.DopingResult;
            record.MedicalResult = request.MedicalResult;
            record.FailReason = request.FailReason;
            record.Notes = request.Notes;
            record.CheckedAt = DateTime.UtcNow;

            // Update horse health status based on check result
            horse.HealthStatus = request.MedicalResult == "Fail"
                ? (request.DopingResult == "Positive" ? "Sick" : "Injured")
                : "Healthy";

            await _repository.SaveChangesAsync();

            // Send notification to Owner
            var ownerId = horse.OwnerId;
            var horseName = horse.Name;
            if (request.MedicalResult == "Fail")
            {
                var failTitle = "Periodic medical check failed";
                var failContent = $"Your horse {horseName} did not pass the periodic medical check due to: {request.FailReason}.";
                await _notificationService.SendNotificationToUserAsync(
                    ownerId, failTitle, failContent, "Medical", (int?)horse.HorseId, null, "/owner/horses");
            }
            else
            {
                var passTitle = "Medical check passed (Healthy)";
                var passContent = $"Your horse {horseName} has passed the periodic medical check and recovered (Healthy).";
                await _notificationService.SendNotificationToUserAsync(
                    ownerId, passTitle, passContent, "Medical", (int?)horse.HorseId, null, "/owner/horses");
            }

            var populatedRecord = await _repository.GetByIdAsync(record.Id);
            return Map(populatedRecord ?? record);
        }

        // --- PATH B: Tournament Registration Check (Existing Logic) ---
        var validCheckType = new[] { "Initial", "ReCheck" };
        if (!validCheckType.Contains(request.CheckType))
            throw new ArgumentException("CheckType must be 'Initial' or 'ReCheck'.");

        var registration = await _registrationRepository.GetByIdAsync(request.RegistrationId.Value);
        if (registration == null)
            throw new ArgumentException($"Registration with ID {request.RegistrationId.Value} does not exist.");

        if (!string.Equals(registration.Status, "PendingVet", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Registration must be pending medical check before an initial medical check can be performed.");

        if (request.CheckType == "Initial")
        {
            var existing = await _repository.GetByRegistrationIdAsync(request.RegistrationId.Value);
            if (existing != null && existing.Any(r => r.CheckType == "Initial"))
                throw new ArgumentException("An initial medical check record already exists for this registration.");
        }

        var newRecord = new MedicalCheckRecord
        {
            RegistrationId = request.RegistrationId.Value,
            HorseId        = registration.HorseId,
            UserId         = performedByUserId,
            CheckType      = request.CheckType,
            Weight         = request.Weight,
            Temperature    = request.Temperature,
            HeartRate      = request.HeartRate,
            DopingResult   = request.DopingResult,
            MedicalResult  = request.MedicalResult,
            FailReason     = request.FailReason,
            Notes          = request.Notes,
            CheckedAt      = DateTime.UtcNow,
        };

        if (registration.Horse != null)
        {
            registration.Horse.HealthStatus = request.MedicalResult == "Fail"
                ? (request.DopingResult == "Positive" ? "Sick" : "Injured")
                : "Healthy";
        }

        registration.Status = request.MedicalResult == "Pass" ? "Pending" : "Rejected";

        await _repository.AddAsync(newRecord);
        await _repository.SaveChangesAsync();

        if (registration.Horse != null)
        {
            var ownerId = registration.Horse.OwnerId;
            var horseName = registration.Horse.Name;
            var tournamentName = registration.Tournament?.Name ?? "Tournament";
            if (request.MedicalResult == "Fail")
            {
                var failTitle = "Medical check failed";
                var failContent = $"Your horse {horseName} did not meet the medical requirements for tournament {tournamentName}. Reason: {request.FailReason}.";
                await _notificationService.SendNotificationToUserAsync(
                    ownerId, failTitle, failContent, "Medical", (int?)registration.RegistrationId, null, "/owner/registrations");

                var ownerEmail = registration.Horse.Owner?.Email;
                if (!string.IsNullOrWhiteSpace(ownerEmail))
                {
                    try
                    {
                        var emailBody = $@"
                            <h2>Medical check result notification</h2>
                            <p>Hello,</p>
                            <p>We regret to inform you that your horse <strong>{horseName}</strong> did not pass the medical check for tournament <strong>{tournamentName}</strong>.</p>
                            <p><strong>Reason:</strong> {request.FailReason}</p>
                            <p><strong>Vet notes:</strong> {request.Notes ?? "None"}</p>
                            <br/>
                            <p>Best regards,<br/>Horse Racing Management</p>";
                        await _emailService.SendEmailAsync(ownerEmail, failTitle, emailBody);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EMAIL ERROR] Failed to send email to {ownerEmail}: {ex.Message}");
                    }
                }
            }
            else
            {
                var passTitle = "Medical check passed";
                var passContent = $"Your horse {horseName} has passed the medical check for tournament {tournamentName}.";
                await _notificationService.SendNotificationToUserAsync(
                    ownerId, passTitle, passContent, "Medical", (int?)registration.RegistrationId, null, "/owner/registrations");

                try
                {
                    var ownerName = registration.Horse.Owner != null
                        ? (registration.Horse.Owner.FullName ?? registration.Horse.Owner.Username)
                        : "Horse Owner";
                    var adminIds = await _registrationRepository.GetAdminUserIdsAsync();
                    foreach (var adminId in adminIds)
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            adminId,
                            "Horse Eligible for Tournament",
                            $"Horse '{horseName}', owned by '{ownerName}', is now eligible to participate in tournament '{tournamentName}'.",
                            "Medical",
                            (int?)registration.RegistrationId,
                            null,
                            "/admin/registrations"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NOTIFICATION ERROR] Failed to send medical pass notification to admin: {ex.Message}");
                }
            }
        }

        var populatedResult = await _repository.GetByIdAsync(newRecord.Id);
        return Map(populatedResult ?? newRecord);
    }

    public async Task<MedicalCheckResponse> UpdateAsync(long id, UpdateMedicalCheckRequest request)
    {
        var record = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Medical check record with ID {id} not found.");

        if (request.Weight.HasValue)
        {
            if (request.Weight.Value <= 0)
                throw new ArgumentException("Weight must be greater than zero.");
            record.Weight = request.Weight.Value;
        }

        if (request.Temperature.HasValue) record.Temperature = request.Temperature;
        if (request.HeartRate.HasValue)   record.HeartRate   = request.HeartRate;
        if (request.Notes is not null)    record.Notes       = request.Notes;

        if (request.DopingResult is not null)
        {
            var valid = new[] { "Negative", "Positive" };
            if (!valid.Contains(request.DopingResult))
                throw new ArgumentException("DopingResult must be 'Negative' or 'Positive'.");
            record.DopingResult = request.DopingResult;
        }

        if (request.MedicalResult is not null)
        {
            var valid = new[] { "Pass", "Fail" };
            if (!valid.Contains(request.MedicalResult))
                throw new ArgumentException("MedicalResult must be 'Pass' or 'Fail'.");

            // ✅ Business Rule: Validate vital signs thresholds before allowing Pass on update
            if (string.Equals(request.MedicalResult, "Pass", StringComparison.OrdinalIgnoreCase))
            {
                var effectiveTemp   = request.Temperature ?? record.Temperature;
                var effectiveHr     = request.HeartRate   ?? record.HeartRate;
                var effectiveWeight = request.Weight      ?? record.Weight;
                var effectiveDoping = request.DopingResult ?? record.DopingResult;
                ValidatePassEligibility(effectiveTemp, effectiveHr, effectiveWeight, effectiveDoping);
            }

            record.MedicalResult = request.MedicalResult;
        }

        _repository.Update(record);
        await _repository.SaveChangesAsync();

        if (request.MedicalResult == "Fail" && record.Registration?.Horse?.Owner?.Email != null)
        {
            var horseName = record.Registration.Horse.Name;
            var tournamentName = record.Registration.Tournament?.Name ?? "Tournament";
            var failTitle = "Medical Check Result Update: FAILED";
            var failReason = record.FailReason ?? "No specific reason provided";
            var notes = request.Notes ?? record.Notes;

            try
            {
                var emailBody = $@"
                    <h2>Medical check result update</h2>
                    <p>Hello,</p>
                    <p>The medical check record of your horse <strong>{horseName}</strong> for tournament <strong>{tournamentName}</strong> has been updated by the vet with the result <strong>FAILED</strong>.</p>
                    <p><strong>Reason:</strong> {failReason}</p>
                    <p><strong>Notes:</strong> {notes ?? "None"}</p>
                    <br/>
                    <p>Best regards,<br/>Horse Racing Management</p>";
                await _emailService.SendEmailAsync(record.Registration.Horse.Owner.Email, failTitle, emailBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send update email to {record.Registration.Horse.Owner.Email}: {ex.Message}");
            }
        }

        var populated = await _repository.GetByIdAsync(record.Id);
        return Map(populated ?? record);
    }

    public async Task DeleteAsync(long id)
    {
        var record = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Medical check record with ID {id} not found.");

        _repository.Delete(record);
        await _repository.SaveChangesAsync();
    }

    public async Task<IEnumerable<PendingRegistrationResponse>> GetPendingRegistrationsAsync()
    {
        var list = new List<PendingRegistrationResponse>();

        // 1. Get pending tournament registrations
        var regs = await _repository.GetPendingRegistrationsForChecksAsync();
        foreach (var r in regs)
        {
            list.Add(new PendingRegistrationResponse
            {
                RegistrationId = r.RegistrationId,
                MedicalRecordId = null,
                HorseId        = r.HorseId,
                HorseName      = r.Horse?.Name ?? string.Empty,
                TournamentName = r.Tournament?.Name ?? string.Empty,
                OwnerName      = r.Horse?.Owner != null ? (r.Horse.Owner.FullName ?? r.Horse.Owner.Username) : string.Empty,
                RegisteredAt   = r.RegisteredAt,
                InspectionType = "Tournament"
            });
        }

        // 2. Get pending general/recovery health checks
        var generalChecks = await _repository.GetPendingGeneralChecksAsync();
        foreach (var g in generalChecks)
        {
            list.Add(new PendingRegistrationResponse
            {
                RegistrationId = null,
                MedicalRecordId = g.Id,
                HorseId        = g.HorseId ?? 0,
                HorseName      = g.Horse?.Name ?? string.Empty,
                TournamentName = "Normal Health Check",
                OwnerName      = g.Horse?.Owner != null ? (g.Horse.Owner.FullName ?? g.Horse.Owner.Username) : string.Empty,
                RegisteredAt   = g.CheckedAt,
                InspectionType = "General"
            });
        }

        return list.OrderByDescending(x => x.RegisteredAt);
    }

    // ─── Re-Examination Workflow ──────────────────────────────────────────────
    public async Task<RecheckResultResponse> PerformRecheckAsync(int vetUserId, RecheckMedicalRequest request)
    {
        // 1. Validate input
        if (request.Weight <= 0)
            throw new ArgumentException("Weight must be greater than zero.");

        var validDoping  = new[] { "Negative", "Positive" };
        var validMedical = new[] { "Pass", "Fail" };

        if (!validDoping.Contains(request.DopingResult))
            throw new ArgumentException("DopingResult must be 'Negative' or 'Positive'.");
        if (!validMedical.Contains(request.MedicalResult))
            throw new ArgumentException("MedicalResult must be 'Pass' or 'Fail'.");
        if (request.MedicalResult == "Fail" && string.IsNullOrWhiteSpace(request.FailReason))
            throw new ArgumentException("FailReason is required when MedicalResult is 'Fail'.");

        // ✅ Business Rule: Validate vital signs thresholds before allowing Pass in recheck
        if (string.Equals(request.MedicalResult, "Pass", StringComparison.OrdinalIgnoreCase))
            ValidatePassEligibility(request.Temperature, request.HeartRate, request.Weight, request.DopingResult);

        // 2. Validate registration exists and is eligible for re-check
        var registration = await _repository.GetRegistrationWithDetailsAsync(request.RegistrationId)
            ?? throw new ArgumentException($"Registration with ID {request.RegistrationId} does not exist.");

        var eligibleStatuses = new[] { "Approved", "Qualified" };
        if (!eligibleStatuses.Any(s => string.Equals(registration.Status, s, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Registration status '{registration.Status}' is not eligible for re-examination. Must be Approved or Qualified.");

        await using var transaction = await _repository.BeginTransactionAsync();
        try
        {
            // 3. Create a NEW MedicalCheckRecord (NEVER overwrite previous records)
            var newRecord = new MedicalCheckRecord
            {
                RegistrationId = request.RegistrationId,
                UserId         = vetUserId,
                CheckType      = "ReCheck",
                Weight         = request.Weight,
                Temperature    = request.Temperature,
                HeartRate      = request.HeartRate,
                DopingResult   = request.DopingResult,
                MedicalResult  = request.MedicalResult,
                FailReason     = request.FailReason,
                Notes          = request.Notes,
                CheckedAt      = DateTime.UtcNow,
            };

            await _repository.AddAsync(newRecord);

            // 4. If horse PASSED → no withdrawal needed
            if (request.MedicalResult == "Pass")
            {
                if (registration.Horse != null)
                {
                    registration.Horse.HealthStatus = "Healthy";
                }
                await _repository.SaveChangesAsync();
                await transaction.CommitAsync();

                var passRecord = await _repository.GetByIdAsync(newRecord.Id);
                return new RecheckResultResponse
                {
                    MedicalRecord      = Map(passRecord ?? newRecord),
                    HorseWithdrawn     = false,
                    RegistrationStatus = registration.Status,
                    Message            = $"Re-inspection passed. Horse '{registration.Horse?.Name}' continues to compete."
                };
            }

            // 5. Horse FAILED → withdrawal workflow
            if (registration.Horse != null)
            {
                registration.Horse.HealthStatus = request.DopingResult == "Positive" ? "Sick" : "Injured";
            }

            // 5a. Update Registration → Disqualified
            registration.Status = "Disqualified";
            _repository.UpdateRegistration(registration);

            // 5b. Find active RaceEntry
            var raceEntry = await _repository.GetActiveRaceEntryByRegistrationIdAsync(request.RegistrationId);
            var withdrawStatus = "None";
            var withdrawReason = request.FailReason ?? "FailedMedicalReCheck";

            if (raceEntry != null)
            {
                var race = await _repository.GetRaceByRaceEntryIdAsync(raceEntry.RaceEntryId);
                var alreadyFinalStatuses = new[] { "Withdrawn", "Scratch", "DNF", "Disqualified", "Finished" };

                if (race != null && !alreadyFinalStatuses.Any(s => string.Equals(raceEntry.Status, s, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.Equals(race.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                    {
                        // Race in progress → DNF
                        raceEntry.Status         = "DNF";
                        raceEntry.WithdrawReason = withdrawReason;
                        raceEntry.WithdrawTime   = DateTime.UtcNow;
                        withdrawStatus = "DNF";
                    }
                    else
                    {
                        // Race not started → Withdrawn
                        raceEntry.Status         = "Withdrawn";
                        raceEntry.WithdrawReason = withdrawReason;
                        raceEntry.WithdrawTime   = DateTime.UtcNow;
                        withdrawStatus = "Withdrawn";
                    }

                    _repository.UpdateRaceEntry(raceEntry);
                }
            }

            await _repository.SaveChangesAsync();

            // 6. Send notifications
            var horseName      = registration.Horse?.Name ?? $"RegistrationId #{registration.RegistrationId}";
            var tournamentName = registration.Tournament?.Name ?? string.Empty;
            var failTitle      = "⚠️ Horse disqualified due to medical check";
            var failContent    = $"Horse {horseName} failed the medical re-inspection in tournament {tournamentName}. Reason: {withdrawReason}.";

            // Notify owner
            var ownerId = await _repository.GetOwnerUserIdByRegistrationAsync(request.RegistrationId);
            if (ownerId.HasValue)
                await _notificationService.SendNotificationToUserAsync(
                    ownerId.Value, failTitle, failContent, "Medical", (int?)raceEntry?.RaceEntryId);

            var ownerEmail = registration.Horse?.Owner?.Email;
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                try
                {
                    var emailBody = $@"
                        <h2>Re-check result notification</h2>
                        <p>Hello,</p>
                        <p>We regret to inform you that your horse <strong>{horseName}</strong> did not pass the re-examination for tournament <strong>{tournamentName}</strong>.</p>
                        <p><strong>Result:</strong> {withdrawReason}</p>
                        <p><strong>Vet notes:</strong> {request.Notes ?? "None"}</p>
                        <p>Your horse has been <strong>disqualified from the race (Withdrawn/DNF)</strong> according to the regulations.</p>
                        <br/>
                        <p>Best regards,<br/>Horse Racing Management</p>";
                    await _emailService.SendEmailAsync(ownerEmail, failTitle, emailBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL ERROR] Failed to send recheck email to {ownerEmail}: {ex.Message}");
                }
            }

            // Notify jockey & referees & bettors (if race entry exists)
            if (raceEntry != null)
            {
                var jockeyId = await _repository.GetJockeyUserIdByRaceEntryAsync(raceEntry.RaceEntryId);
                if (jockeyId.HasValue)
                    await _notificationService.SendNotificationToUserAsync(
                        jockeyId.Value, failTitle,
                        $"Horse {horseName} in tournament {tournamentName} has been withdrawn due to failed medical check. Your contract has been affected.",
                        "Medical", (int?)raceEntry.RaceEntryId);

                var refereeIds = await _repository.GetRefereeUserIdsByRaceIdAsync(raceEntry.RaceId);
                foreach (var refereeId in refereeIds)
                    await _notificationService.SendNotificationToUserAsync(
                        refereeId, failTitle, failContent, "Medical", (int?)raceEntry.RaceEntryId);

                var bettorIds = await _repository.GetBettorUserIdsByRaceIdAsync(raceEntry.RaceId);
                foreach (var bettorId in bettorIds)
                    await _notificationService.SendNotificationToUserAsync(
                        bettorId,
                        "⚠️ Bet Update — Horse withdrawn from race",
                        $"Horse {horseName} has been withdrawn from the race in tournament {tournamentName} due to failing the medical check. Please check your bet status.",
                        "Bet", (int?)raceEntry.RaceId);
            }

            await transaction.CommitAsync();

            var populated = await _repository.GetByIdAsync(newRecord.Id);
            var actionText = withdrawStatus == "DNF" ? "marked as DNF" : "withdrawn from the race";
            return new RecheckResultResponse
            {
                MedicalRecord      = Map(populated ?? newRecord),
                HorseWithdrawn     = true,
                WithdrawStatus     = withdrawStatus == "None" ? null : withdrawStatus,
                WithdrawReason     = withdrawReason,
                RegistrationStatus = "Disqualified",
                Message            = $"Re-inspection failed. Horse '{horseName}' has been {actionText}."
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ─── Assigned Race Entries ────────────────────────────────────────────────
    public async Task<IEnumerable<AssignedRaceEntryResponse>> GetAssignedRaceEntriesAsync()
    {
        var entries = await _repository.GetAssignedRaceEntriesAsync();
        var resultList = new List<AssignedRaceEntryResponse>();

        foreach (var re in entries)
        {
            MedicalCheckRecord? latestCheck = null;
            if (re.Registration != null)
            {
                latestCheck = await _repository.GetLatestByHorseIdAsync(re.Registration.HorseId);
            }

            resultList.Add(new AssignedRaceEntryResponse
            {
                RaceEntryId       = re.RaceEntryId,
                RaceId            = re.RaceId,
                RaceName          = re.Race?.Name,
                RaceDate          = re.Race?.RaceDate ?? default,
                RaceStatus        = re.Race?.Status ?? string.Empty,
                LaneNo            = re.LaneNo,
                RaceEntryStatus   = re.Status,
                RegistrationId    = re.RegistrationId,
                HorseName         = re.Registration?.Horse?.Name,
                OwnerName         = re.Registration?.Horse?.Owner != null
                                      ? (re.Registration.Horse.Owner.FullName ?? re.Registration.Horse.Owner.Username)
                                      : null,
                JockeyName        = re.JockeyProfile?.User != null
                                      ? (re.JockeyProfile.User.FullName ?? re.JockeyProfile.User.Username)
                                      : null,
                TournamentName    = re.Registration?.Tournament?.Name,
                LastMedicalResult = latestCheck?.MedicalResult ?? (re.Registration?.Horse?.HealthStatus == "Sick" || re.Registration?.Horse?.HealthStatus == "Injured" ? "Fail" : null),
                LastCheckType     = latestCheck?.CheckType,
                LastCheckedAt     = latestCheck?.CheckedAt,
            });
        }

        return resultList;
    }

    public async Task<IEnumerable<UnhealthyHorseResponse>> GetUnhealthyHorsesAsync()
    {
        var horses = await _repository.GetUnhealthyHorsesAsync();
        return horses.Select(h => new UnhealthyHorseResponse
        {
            HorseId = h.HorseId,
            Name = h.Name,
            Age = h.Age,
            Gender = h.Gender,
            Breed = h.Breed,
            HealthStatus = h.HealthStatus,
            OwnerId = h.OwnerId,
            OwnerName = h.Owner != null ? (h.Owner.FullName ?? h.Owner.Username) : string.Empty
        });
    }

    public async Task<bool> RecoverHorseAsync(long horseId)
    {
        var horse = await _repository.GetHorseByIdAsync(horseId);
        if (horse == null)
        {
            throw new KeyNotFoundException($"Horse with ID {horseId} not found.");
        }

        if (horse.HealthStatus == "Healthy" || horse.HealthStatus == "Good")
        {
            return false;
        }

        horse.HealthStatus = "Healthy";
        _repository.UpdateHorse(horse);
        await _repository.SaveChangesAsync();

        // Send notification
        var title = "Horse has recovered";
        var content = $"Your horse {horse.Name} has been confirmed recovered by the vet (status: Healthy). You can now register it for new tournaments.";
        await _notificationService.SendNotificationToUserAsync(
            horse.OwnerId, title, content, "Medical", null, null, "/owner/horses");

        return true;
    }

    public async Task<bool> RequestRecoveryCheckAsync(int ownerUserId, long horseId)
    {
        var horse = await _repository.GetHorseByIdAsync(horseId);
        if (horse == null)
        {
            throw new KeyNotFoundException($"Horse with ID {horseId} not found.");
        }

        if (horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse.");
        }

        if (horse.HealthStatus == "Healthy" || horse.HealthStatus == "Good")
        {
            throw new InvalidOperationException("Horse is already healthy.");
        }

        // Create a pending medical check record if one doesn't exist
        var existingPending = await _repository.GetPendingGeneralCheckByHorseIdAsync(horseId);
        if (existingPending == null)
        {
            var pendingCheck = new MedicalCheckRecord
            {
                HorseId = horseId,
                RegistrationId = null,
                CheckType = "General",
                MedicalResult = "Pending",
                UserId = ownerUserId,
                Weight = 0,
                CheckedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(pendingCheck);
            await _repository.SaveChangesAsync();
        }

        // Get all Vet user IDs to notify
        var vetIds = await _repository.GetVeterinarianUserIdsAsync();
        if (vetIds.Any())
        {
            var title = "Recovery Inspection Request";
            var content = $"Owner {horse.Owner?.FullName ?? horse.Owner?.Username ?? "Owner"} requested a health recovery check for horse {horse.Name} (Current status: {horse.HealthStatus}).";
            foreach (var vetId in vetIds)
            {
                await _notificationService.SendNotificationToUserAsync(
                    vetId, title, content, "Medical", null, null, "/vet/medical-check");
            }
        }

        return true;
    }
}
