using System.Threading.Tasks;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Application.Features.Demo.Interfaces;

public interface IDemoService
{
    Task<Tournament> SetupDemoTournamentAsync();
}
