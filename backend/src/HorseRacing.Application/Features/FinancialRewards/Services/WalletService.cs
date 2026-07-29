using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.FinancialRewards.DTOs;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.FinancialRewards.Services;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _transactionRepository;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public WalletService(
        IWalletRepository walletRepository,
        IWalletTransactionRepository transactionRepository,
        INotificationService notificationService,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _notificationService = notificationService;
        _userRepository = userRepository;
        _emailService = emailService;
    }

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            var userExists = await _walletRepository.UserExistsAsync(userId);
            if (!userExists)
            {
                throw new ArgumentException($"User with ID {userId} does not exist.");
            }

            wallet = new Wallet
            {
                UserId = userId,
                Balance = 0
            };
            await _walletRepository.AddAsync(wallet);
            await _walletRepository.SaveChangesAsync();
        }
        return wallet;
    }

    public async Task<WalletBalanceResponse> GetBalanceAsync(int userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return new WalletBalanceResponse
        {
            WalletId = wallet.WalletId,
            UserId = wallet.UserId,
            Balance = wallet.Balance
        };
    }

    public async Task<WalletBalanceResponse> DepositAsync(int userId, DepositRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be greater than zero.");
        }

        var wallet = await GetOrCreateWalletAsync(userId);
        wallet.Balance += request.Amount;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.WalletId,
            Amount = request.Amount,
            Type = "Deposit",
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.AddAsync(transaction);
        
        try
        {
            await _transactionRepository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            throw new InvalidOperationException("Your wallet balance was modified by another transaction. Please try again.");
        }

        // Create wallet notification
        try
        {
            await _notificationService.SendNotificationToUserAsync(
                userId,
                "Deposit Successful",
                $"You successfully deposited {request.Amount:N2}$ into your wallet. New balance: {wallet.Balance:N2}$.",
                "Wallet",
                actionUrl: "/spectator/wallet/overview"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WalletService.DepositAsync] Error sending notification: {ex}");
        }

        // Send Email Receipt
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                 var subject = "Deposit Receipt: Deposit successful";
                 var body = $@"
                     <h2>Deposit Successful!</h2>
                     <p>Hello {user.FullName},</p>
                     <p>You have successfully deposited <strong>{request.Amount:N2}$</strong> into your wallet.</p>
                     <p>Your new balance is: <strong>{wallet.Balance:N2}$</strong>.</p>
                     <p>Thank you for using our services.</p>
                 ";
                try { await _emailService.SendEmailAsync(user.Email, subject, body); } catch { /* ignore email error */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WalletService.DepositAsync] Error sending email receipt: {ex}");
        }

        return new WalletBalanceResponse
        {
            WalletId = wallet.WalletId,
            UserId = wallet.UserId,
            Balance = wallet.Balance
        };
    }

    public async Task<WalletBalanceResponse> WithdrawAsync(int userId, WithdrawRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Withdrawal amount must be greater than zero.");
        }

        var wallet = await GetOrCreateWalletAsync(userId);
        if (wallet.Balance < request.Amount)
        {
            throw new InvalidOperationException("Insufficient balance.");
        }

        wallet.Balance -= request.Amount;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.WalletId,
            Amount = -request.Amount,
            Type = "Withdraw",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.AddAsync(transaction);
        
        try
        {
            await _transactionRepository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            throw new InvalidOperationException("Your wallet balance was modified by another transaction. Please try again.");
        }

        // Create wallet notification
        try
        {
            await _notificationService.SendNotificationToUserAsync(
                userId,
                "Withdrawal Pending",
                $"You requested to withdraw {request.Amount:N2}$ from your wallet. Your current balance is {wallet.Balance:N2}$. This request is pending Admin approval.",
                "Wallet",
                actionUrl: "/spectator/wallet/overview"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WalletService.WithdrawAsync] Error sending notification: {ex}");
        }

        // Send Email Receipt
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                 var subject = "Withdrawal Request Received";
                 var body = $@"
                     <h2>Withdrawal Request Pending</h2>
                     <p>Hello {user.FullName},</p>
                     <p>We have received your request to withdraw <strong>{request.Amount:N2}$</strong> from your wallet.</p>
                     <p>Your current balance after deduction is: <strong>{wallet.Balance:N2}$</strong>.</p>
                     <p>This request is currently pending Admin approval. You will be notified once it is processed.</p>
                     <p>Thank you for using our services.</p>
                 ";
                try { await _emailService.SendEmailAsync(user.Email, subject, body); } catch { /* ignore email error */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WalletService.WithdrawAsync] Error sending email receipt: {ex}");
        }

        return new WalletBalanceResponse
        {
            WalletId = wallet.WalletId,
            UserId = wallet.UserId,
            Balance = wallet.Balance
        };
    }

    public async Task<IEnumerable<TransactionHistoryResponse>> GetTransactionHistoryAsync(int userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        var transactions = await _transactionRepository.GetByWalletIdAsync(wallet.WalletId);

        return transactions.Select(t => new TransactionHistoryResponse
        {
            Id = t.TransactionId,
            WalletId = t.WalletId,
            Amount = t.PaymentMethod == "VNPay" ? t.Amount / 250m : t.Amount,
            Type = t.Type,
            Description = t.Description,
            CreatedAt = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc)
        });
    }
}
