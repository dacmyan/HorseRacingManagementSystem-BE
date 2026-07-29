using System;
using System.Threading.Tasks;
using HorseRacing.Application.Features.FinancialRewards.DTOs;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Services;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Tests.Unit
{
    public class WalletServiceTests
    {
        private readonly Mock<IWalletRepository> _walletRepositoryMock;
        private readonly Mock<IWalletTransactionRepository> _transactionRepositoryMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly WalletService _walletService;

        public WalletServiceTests()
        {
            _walletRepositoryMock = new Mock<IWalletRepository>();
            _transactionRepositoryMock = new Mock<IWalletTransactionRepository>();
            _notificationServiceMock = new Mock<INotificationService>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _emailServiceMock = new Mock<IEmailService>();

            _walletService = new WalletService(
                _walletRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _notificationServiceMock.Object,
                _userRepositoryMock.Object,
                _emailServiceMock.Object
            );
        }

        [Fact]
        public async Task DepositAsync_WhenConcurrencyExceptionOccurs_ThrowsInvalidOperationException()
        {
            // Arrange
            var userId = 1;
            var request = new DepositRequest { Amount = 100 };
            var wallet = new Wallet { WalletId = 1, UserId = userId, Balance = 0 };

            _walletRepositoryMock.Setup(repo => repo.GetByUserIdAsync(userId))
                .ReturnsAsync(wallet);

            _transactionRepositoryMock.Setup(repo => repo.SaveChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict", (Exception)null));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _walletService.DepositAsync(userId, request));

            Assert.Equal("Your wallet balance was modified by another transaction. Please try again.", exception.Message);
        }

        [Fact]
        public async Task WithdrawAsync_WhenConcurrencyExceptionOccurs_ThrowsInvalidOperationException()
        {
            // Arrange
            var userId = 1;
            var request = new WithdrawRequest { Amount = 50 };
            var wallet = new Wallet { WalletId = 1, UserId = userId, Balance = 100 };

            _walletRepositoryMock.Setup(repo => repo.GetByUserIdAsync(userId))
                .ReturnsAsync(wallet);

            _transactionRepositoryMock.Setup(repo => repo.SaveChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict", (Exception)null));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _walletService.WithdrawAsync(userId, request));

            Assert.Equal("Your wallet balance was modified by another transaction. Please try again.", exception.Message);
        }
    }
}
