using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.HorseManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.HorseManagement.Services;

public class HorseService : IHorseService
{
    private readonly IHorseRepository _horseRepository;

    public HorseService(IHorseRepository horseRepository)
    {
        _horseRepository = horseRepository;
    }

    private HorseDetailResponse MapToDetailResponse(Horse horse)
    {
        return new HorseDetailResponse
        {
            Id = (int)horse.HorseId,
            Name = horse.Name,
            Age = horse.Age,
            Gender = horse.Gender,
            Breed = horse.Breed,
            HealthStatus = horse.HealthStatus,
            OwnerId = horse.OwnerId,
            OwnerName = horse.Owner?.FullName ?? "Unknown",
            Documents = horse.Documents.Select(d => new HorseDocumentResponse
            {
                Id = d.Id,
                DocumentType = d.DocumentType,
                DocumentUrl = d.DocumentUrl,
                UploadedAt = d.UploadedAt
            }).ToList(),
            Statistic = horse.Statistic != null ? new HorseStatisticResponse
            {
                Id = horse.Statistic.Id,
                HorseId = (int)horse.Statistic.HorseId,
                TotalRaces = horse.Statistic.TotalRaces,
                TotalWins = horse.Statistic.TotalWins,
                TotalSecondPlaces = horse.Statistic.TotalSecondPlaces,
                TotalThirdPlaces = horse.Statistic.TotalThirdPlaces,
                AverageSpeed = horse.Statistic.AverageSpeed,
                UpdatedAt = horse.Statistic.UpdatedAt
            } : null
        };
    }

    public async Task<IEnumerable<HorseDetailResponse>> GetHorsesByOwnerAsync(int ownerUserId)
    {
        var horses = await _horseRepository.GetByOwnerIdAsync(ownerUserId);
        return horses.Select(MapToDetailResponse);
    }

    public async Task<HorseDetailResponse?> GetHorseByIdAsync(int id, int ownerUserId)
    {
        var horse = await _horseRepository.GetByIdAsync(id);
        if (horse == null || horse.OwnerId != ownerUserId)
        {
            return null;
        }
        return MapToDetailResponse(horse);
    }

    public async Task<HorseDetailResponse> CreateHorseAsync(int ownerUserId, RegisterHorseRequest request)
    {
        var horse = new Horse
        {
            Name = request.Name,
            Age = request.Age,
            Gender = request.Gender,
            Breed = request.Breed,
            OwnerId = ownerUserId,
            HealthStatus = "Healthy"
        };

        await _horseRepository.AddAsync(horse);
        await _horseRepository.SaveChangesAsync();

        // Automatically initialize empty statistics for the new horse
        var stats = new HorseStatistic
        {
            HorseId = (int)horse.HorseId,
            TotalRaces = 0,
            TotalWins = 0,
            TotalSecondPlaces = 0,
            TotalThirdPlaces = 0,
            AverageSpeed = 0m,
            UpdatedAt = DateTime.UtcNow
        };
        await _horseRepository.AddStatisticAsync(stats);
        await _horseRepository.SaveChangesAsync();

        // Fetch again to include populated relations
        var createdHorse = await _horseRepository.GetByIdAsync((int)horse.HorseId);
        return MapToDetailResponse(createdHorse ?? horse);
    }

    public async Task<HorseDetailResponse> UpdateHorseAsync(int id, int ownerUserId, UpdateHorseRequest request)
    {
        var horse = await _horseRepository.GetByIdAsync(id);
        if (horse == null)
        {
            throw new ArgumentException($"Horse with ID {id} not found.");
        }
        if (horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse.");
        }

        horse.Name = request.Name;
        horse.Age = request.Age;
        horse.Gender = request.Gender;
        horse.Breed = request.Breed;
        // horse.HealthStatus = request.HealthStatus; // Disabled: Only Vet can update health status via medical checkups

        _horseRepository.Update(horse);
        await _horseRepository.SaveChangesAsync();

        return MapToDetailResponse(horse);
    }

    public async Task DeleteHorseAsync(int id, int ownerUserId)
    {
        var horse = await _horseRepository.GetByIdAsync(id);
        if (horse == null)
        {
            throw new ArgumentException($"Horse with ID {id} not found.");
        }
        if (horse.OwnerId != ownerUserId)
        {
            throw new InvalidOperationException("Access denied. You do not own this horse.");
        }

        if (await _horseRepository.HasHistoricalOrActiveDependenciesAsync(id))
        {
            throw new InvalidOperationException("Cannot delete this horse because it has associated racing history or active contracts. Please consider updating its status instead.");
        }

        _horseRepository.Delete(horse);
        await _horseRepository.SaveChangesAsync();
    }
}
