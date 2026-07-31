using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HorseRacing.Infrastructure.Persistence;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.BettingEngine.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.MedicalCheck.Interfaces;
using HorseRacing.Application.Features.MedicalCheck.Services;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Infrastructure.Repositories;
using HorseRacing.Infrastructure.ExternalServices;

namespace HorseRacing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHorseRepository, HorseRepository>();
        services.AddScoped<IJockeyContractRepository, JockeyContractRepository>();
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        services.AddScoped<IBetRepository, BetRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IPrizeRepository, PrizeRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<IRaceRepository, RaceRepository>();
        services.AddScoped<IRefereeAssignmentRepository, RefereeAssignmentRepository>();
        services.AddScoped<IViolationRepository, ViolationRepository>();
        services.AddScoped<IRefereeReportRepository, RefereeReportRepository>();
        services.AddScoped<IResultRepository, ResultRepository>();
        services.AddScoped<IPredictionRepository, PredictionRepository>();
        services.AddScoped<IJockeyRepository, JockeyRepository>();
        services.AddScoped<IRefereeDashboardRepository, RefereeDashboardRepository>();
        services.AddScoped<IMedicalCheckRepository, MedicalCheckRepository>();
        services.AddScoped<HorseRacing.Application.Features.Public.Interfaces.IPublicQueryRepository, PublicQueryRepository>();
        services.AddScoped<IOwnerDashboardRepository, OwnerDashboardRepository>();
        services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
        services.AddScoped<IAdminActionRepository, AdminActionRepository>();
        services.AddScoped<IMedicalCheckService, MedicalCheckService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<DataSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
