using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Application.Features.OfficiatingAndResults.Services;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using Moq;
using Xunit;
using FluentAssertions;

namespace HorseRacing.Tests.Unit;

public class RefereeServiceTests
{
    private readonly Mock<IViolationRepository> _violationRepoMock;
    private readonly Mock<IRefereeReportRepository> _reportRepoMock;
    private readonly Mock<IRefereeDashboardRepository> _dashboardRepoMock;
    private readonly RefereeService _service;

    public RefereeServiceTests()
    {
        _violationRepoMock = new Mock<IViolationRepository>();
        _reportRepoMock = new Mock<IRefereeReportRepository>();
        _dashboardRepoMock = new Mock<IRefereeDashboardRepository>();
        _service = new RefereeService(_violationRepoMock.Object, _reportRepoMock.Object, _dashboardRepoMock.Object);
    }

    [Fact]
    public async Task SubmitReportAsync_ShouldThrowArgumentException_WhenContentIsEmpty()
    {
        // Arrange
        var request = new CreateRefereeReportRequest
        {
            AssignmentId = 1,
            Content = ""
        };

        var assignment = new RaceRefereeAssignment
        {
            AssignmentId = 1,
            RefereeId = 1,
            RaceId = 1,
            RefereeProfile = new RefereeProfile
            {
                RefereeId = 1,
                User = new AppUser
                {
                    UserId = 1,
                    Role = new Role { RoleId = 4, Name = "Referee" }
                }
            }
        };

        _reportRepoMock.Setup(r => r.GetAssignmentByIdAsync(1))
            .ReturnsAsync(assignment);

        // Act
        Func<Task> act = async () => await _service.SubmitReportAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Report content cannot be empty. (Parameter 'Content')");
    }

    [Fact]
    public async Task SubmitReportAsync_ShouldThrowKeyNotFoundException_WhenAssignmentDoesNotExist()
    {
        // Arrange
        var request = new CreateRefereeReportRequest
        {
            AssignmentId = 999,
            Content = "Test Content"
        };

        _reportRepoMock.Setup(r => r.GetAssignmentByIdAsync(999))
            .ReturnsAsync((RaceRefereeAssignment?)null);

        // Act
        Func<Task> act = async () => await _service.SubmitReportAsync(request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Race referee assignment with ID 999 was not found.");
    }

    [Fact]
    public async Task SubmitReportAsync_ShouldResolveAssignment_WhenRaceIdAndRefereeIdAreProvided()
    {
        // Arrange
        var request = new CreateRefereeReportRequest
        {
            RaceId = 5,
            RefereeId = 3,
            Content = "Some report content"
        };

        var assignment = new RaceRefereeAssignment
        {
            AssignmentId = 12,
            RefereeId = 3,
            RaceId = 5,
            RefereeProfile = new RefereeProfile
            {
                RefereeId = 3,
                User = new AppUser
                {
                    UserId = 10,
                    FullName = "Assigned Referee User",
                    Role = new Role { RoleId = 4, Name = "Referee" }
                }
            }
        };

        _reportRepoMock.Setup(r => r.GetAssignmentByRaceAndRefereeAsync(5, 3))
            .ReturnsAsync(assignment);

        // Act
        var response = await _service.SubmitReportAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.AssignmentId.Should().Be(12);
        response.RaceId.Should().Be(5);
        response.RefereeId.Should().Be(3);
        response.RefereeName.Should().Be("Assigned Referee User");
        response.Content.Should().Be("Some report content");

        _reportRepoMock.Verify(r => r.AddReportAsync(It.Is<RefereeReport>(rep => rep.AssignmentId == 12 && rep.Content == "Some report content")), Times.Once);
        _reportRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
