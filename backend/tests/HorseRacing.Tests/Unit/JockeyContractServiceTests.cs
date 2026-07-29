using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Application.Features.ContractAndRegistration.Services;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using Moq;
using Xunit;

namespace HorseRacing.Tests.Unit;

public class JockeyContractServiceTests
{
    private readonly Mock<IJockeyContractRepository> _contractRepoMock;
    private readonly Mock<IHorseRepository> _horseRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ITournamentRepository> _tournamentRepoMock;
    private readonly Mock<IRegistrationRepository> _registrationRepoMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly JockeyContractService _service;

    public JockeyContractServiceTests()
    {
        _contractRepoMock = new Mock<IJockeyContractRepository>();
        _horseRepoMock = new Mock<IHorseRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _notificationMock = new Mock<INotificationService>();
        _tournamentRepoMock = new Mock<ITournamentRepository>();
        _registrationRepoMock = new Mock<IRegistrationRepository>();
        _emailMock = new Mock<IEmailService>();

        _service = new JockeyContractService(
            _contractRepoMock.Object,
            _horseRepoMock.Object,
            _userRepoMock.Object,
            _notificationMock.Object,
            _tournamentRepoMock.Object,
            _registrationRepoMock.Object,
            _emailMock.Object
        );
    }

    [Fact]
    public async Task RespondToContractAsync_ShouldCancelOtherPendingInvitations_WhenJockeyAcceptsContract()
    {
        // Arrange
        int contractId = 1;
        int jockeyId = 5;
        long tournamentId = 10;

        var acceptedContract = new JockeyContract
        {
            ContractId = contractId,
            JockeyId = jockeyId,
            TournamentId = tournamentId,
            Status = "Pending",
            InvitationExpiredAt = DateTime.UtcNow.AddDays(1),
            Horse = new Horse { HorseId = 1, Name = "Champion Horse", OwnerId = 100 },
            Jockey = new AppUser { UserId = jockeyId, FullName = "Jockey A" },
            Tournament = new Tournament 
            { 
                TournamentId = tournamentId, 
                Name = "Gold Cup",
                RegistrationStartDate = DateTime.UtcNow.AddDays(-5),
                StartDate = DateTime.UtcNow.AddDays(-2),
                EndDate = DateTime.UtcNow.AddDays(5)
            }
        };

        var otherPendingContract = new JockeyContract
        {
            ContractId = 2,
            JockeyId = jockeyId,
            TournamentId = tournamentId,
            Status = "Pending",
            InvitationExpiredAt = DateTime.UtcNow.AddDays(1),
            Horse = new Horse { HorseId = 2, Name = "Silver Horse", OwnerId = 200 }
        };

        _contractRepoMock.Setup(r => r.GetByIdAsync(contractId))
            .ReturnsAsync(acceptedContract);

        _contractRepoMock.Setup(r => r.HasActiveContractForJockeyInTournamentAsync(jockeyId, tournamentId))
            .ReturnsAsync(false);

        _contractRepoMock.Setup(r => r.GetActiveContractForHorseAsync(It.IsAny<int>(), It.IsAny<long>()))
            .ReturnsAsync((JockeyContract?)null);

        _contractRepoMock.Setup(r => r.GetOtherPendingContractsForJockeyInTournamentAsync(jockeyId, tournamentId, contractId))
            .ReturnsAsync(new List<JockeyContract> { otherPendingContract });

        _contractRepoMock.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        _notificationMock.Setup(n => n.SendNotificationToUserAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>()
            ))
            .Returns(Task.CompletedTask);

        var request = new RespondToContractRequest { Status = "Accepted" };

        // Act
        var response = await _service.RespondToContractAsync(jockeyId, contractId, request);

        // Assert
        response.Status.Should().Be("Accepted");
        otherPendingContract.Status.Should().Be("Cancelled");

        // Verify other owner was notified
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            200,
            "Jockey invitation cancelled",
            It.Is<string>(s => s.Contains("Silver Horse") && s.Contains("Jockey A") && s.Contains("Gold Cup")),
            "System",
            2,
            null,
            "/owner/jockeys"
        ), Times.Once);
    }
}
