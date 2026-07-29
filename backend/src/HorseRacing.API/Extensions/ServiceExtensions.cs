using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.UserManagement.Services;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.API.SignalR;
using HorseRacing.Application.Features.BettingEngine.Interfaces;
using HorseRacing.Application.Features.BettingEngine.Services;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Services;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.UserManagement.Services;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.Notifications.Services;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.HorseManagement.Services;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.ContractAndRegistration.Services;
using HorseRacing.Application.Features.TournamentAndRacing.Services;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.Services;

namespace HorseRacing.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceExtensions(this IServiceCollection services, IConfiguration configuration)
    {
        // Add AutoMapper using Assembly scanning
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IHorseService, HorseService>();
        services.AddScoped<IHorseDocumentService, HorseDocumentService>();
        services.AddScoped<IJockeyContractService, JockeyContractService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IBettingService, BettingService>();

        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IBetPayoutService, BetPayoutService>();
        services.AddScoped<IPrizePayoutService, PrizePayoutService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationPusher, NotificationPusher>();
        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<IRoundService, RoundService>();
        services.AddScoped<IRaceService, RaceService>();
        services.AddScoped<IRefereeAssignmentService, RefereeAssignmentService>();
        services.AddScoped<IRefereeService, RefereeService>();
        services.AddScoped<IRaceResultService, RaceResultService>();
        services.AddScoped<IPredictionService, PredictionService>();
        services.AddScoped<IJockeyService, JockeyService>();
        services.AddScoped<HorseRacing.Application.Features.Public.Interfaces.IPublicQueryService, HorseRacing.Application.Features.Public.Services.PublicQueryService>();

        services.AddScoped<IOwnerDashboardService, OwnerDashboardService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminActionService, AdminActionService>();

        return services;
    }
}
