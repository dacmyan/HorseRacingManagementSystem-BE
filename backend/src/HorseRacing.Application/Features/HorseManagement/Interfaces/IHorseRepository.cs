using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.HorseManagement.Interfaces;

public interface IHorseRepository
{
    Task<Horse?> GetByIdAsync(int id);
    Task<IEnumerable<Horse>> GetByOwnerIdAsync(int ownerId);
    Task AddAsync(Horse horse);
    void Update(Horse horse);
    void Delete(Horse horse);
    Task<bool> HasActiveDependenciesAsync(long horseId);
    Task<bool> HasHistoricalDependenciesAsync(long horseId);
    Task SaveChangesAsync();

    // Documents
    Task AddDocumentAsync(HorseDocument doc);
    Task<HorseDocument?> GetDocumentByIdAsync(int docId);
    void DeleteDocument(HorseDocument doc);

    // Statistics
    Task<HorseStatistic?> GetStatisticByHorseIdAsync(int horseId);
    Task AddStatisticAsync(HorseStatistic stat);
    void UpdateStatistic(HorseStatistic stat);
}
