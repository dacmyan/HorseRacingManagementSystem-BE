using System.Threading.Tasks;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.API.Services;

public interface IDemoService
{
    Task<Tournament> SetupDemoTournamentAsync();
    Task<Tournament> ResolveDemoTournamentAsync(long tournamentId);
    Task<Tournament> PopulateTournamentAsync(long tournamentId);
    Task<Race> ResolveSingleRaceAsync(long raceId);
}
