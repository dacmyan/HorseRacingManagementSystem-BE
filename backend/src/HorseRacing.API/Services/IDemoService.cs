using System.Threading.Tasks;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.API.Services;

public interface IDemoService
{
    Task<Tournament> SetupDemoTournamentAsync();
    Task<Tournament> StartDemoTournamentAsync(long tournamentId);
    Task<Tournament> PopulateTournamentAsync(long tournamentId, int count);
    Task<Race> StartSingleRaceAsync(long raceId);
}
