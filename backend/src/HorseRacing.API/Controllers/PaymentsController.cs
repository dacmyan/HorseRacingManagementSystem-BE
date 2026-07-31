using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HorseRacing.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IVnPayService _vnPayService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        AppDbContext context,
        IVnPayService vnPayService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _vnPayService = vnPayService;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameIdentifier))
        {
            nameIdentifier = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        }
        return int.Parse(nameIdentifier ?? "0");
    }

    [HttpPost("vnpay/create-deposit")]
    [Authorize]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        // Validate Amount >= 10,000 VND
        if (request.Amount < 10000)
        {
            return BadRequest(new { message = "The minimum deposit amount is 10,000 VND." });
        }

        // Retrieve or create User's Wallet
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        // Generate unique transaction reference (e.g. TRANS_YYYYMMDDHHMMSS_random)
        string timePart = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        string randPart = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
        string txnRef = $"TRANS_{timePart}_{randPart}";

        // Create PENDING WalletTransaction
        var transaction = new WalletTransaction
        {
            WalletId = wallet.WalletId,
            Amount = request.Amount,
            Type = "Deposit",
            Status = "PENDING",
            PaymentMethod = "VNPay",
            GatewayTransactionId = txnRef, // Use GatewayTransactionId to store the unique txnRef for tracking
            Description = string.IsNullOrEmpty(request.OrderInfo) ? "Nap tien vao vi tai khoan" : request.OrderInfo,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deposit transaction created: User {UserId}, Amount {Amount}, Ref {TxnRef}", userId, request.Amount, txnRef);

        // Build VNPay Payment URL
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // Construct dynamic backend return URL based on config (or fallback to current request)
        var baseUrl = _configuration["BaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
        string dynamicReturnUrl = $"{baseUrl}/api/payments/vnpay/return";

        string paymentUrl = _vnPayService.CreatePaymentUrl(txnRef, request.Amount, transaction.Description, ipAddress, dynamicReturnUrl);

        return Ok(new
        {
            paymentUrl,
            transactionReference = txnRef
        });
    }

    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn()
    {
        _logger.LogInformation("VNPay return callback received with parameters: {Query}", Request.QueryString);

        var parameters = new Dictionary<string, string>();
        foreach (var key in Request.Query.Keys)
        {
            parameters.Add(key, Request.Query[key]!);
        }

        // Read Return URL configured in frontend env settings
        string defaultFrontendUrl = "http://localhost:5173/payment/vnpay/return";
        if (Request.Host.Host.Contains("azurewebsites.net"))
        {
            defaultFrontendUrl = "https://horse-tournament-management.vercel.app/payment/vnpay/return";
        }
        var frontendReturnUrl = _configuration["VNPAY_RETURN_URL"] ?? defaultFrontendUrl;

        // Validate hash signature
        bool isValidSignature = _vnPayService.ValidateCallback(parameters);
        if (!isValidSignature)
        {
            _logger.LogWarning("Invalid VNPay signature received in return callback.");
            return Redirect($"{frontendReturnUrl}?status=invalid_signature");
        }

        // Extract parameters
        parameters.TryGetValue("vnp_TxnRef", out var txnRef);
        parameters.TryGetValue("vnp_ResponseCode", out var responseCode);
        parameters.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
        parameters.TryGetValue("vnp_Amount", out var amountStr);
        parameters.TryGetValue("vnp_TransactionNo", out var vnpTransactionNo);
        parameters.TryGetValue("vnp_BankCode", out var bankCode);

        // Perform idempotent processing in return URL as well to ensure robust UX
        if (!string.IsNullOrEmpty(txnRef))
        {
            await ProcessPaymentConfirmationAsync(txnRef, amountStr, responseCode, transactionStatus, vnpTransactionNo, bankCode);
        }

        return Redirect($"{frontendReturnUrl}?vnp_TxnRef={txnRef}&vnp_ResponseCode={responseCode}&vnp_TransactionStatus={transactionStatus}&vnp_Amount={amountStr}");
    }

    [HttpGet("vnpay/ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn()
    {
        _logger.LogInformation("VNPay IPN webhook received with parameters: {Query}", Request.QueryString);

        var parameters = new Dictionary<string, string>();
        foreach (var key in Request.Query.Keys)
        {
            parameters.Add(key, Request.Query[key]!);
        }

        try
        {
            // 1. Verify Signature
            if (!_vnPayService.ValidateCallback(parameters))
            {
                _logger.LogWarning("Invalid VNPay signature received in IPN webhook.");
                return Ok(new { RspCode = "97", Message = "Invalid signature" });
            }

            // 2. Extract and Validate Order Exists
            if (!parameters.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrEmpty(txnRef))
            {
                return Ok(new { RspCode = "01", Message = "Order not found" });
            }

            // 3. Extract check variables
            parameters.TryGetValue("vnp_Amount", out var amountStr);
            parameters.TryGetValue("vnp_ResponseCode", out var responseCode);
            parameters.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
            parameters.TryGetValue("vnp_TransactionNo", out var vnpTransactionNo);
            parameters.TryGetValue("vnp_BankCode", out var bankCode);

            var (rspCode, message) = await ProcessPaymentConfirmationAsync(txnRef, amountStr, responseCode, transactionStatus, vnpTransactionNo, bankCode);

            return Ok(new { RspCode = rspCode, Message = message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in VNPay IPN Handler");
            return Ok(new { RspCode = "99", Message = "Input required data invalid" });
        }
    }

    private async Task<(string RspCode, string Message)> ProcessPaymentConfirmationAsync(
        string txnRef, string? amountStr, string? responseCode, string? transactionStatus, string? vnpTransactionNo, string? bankCode)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == txnRef);

        if (transaction == null)
        {
            return ("01", "Order not found");
        }

        // Verify Amount (vnp_Amount = Amount * 100)
        if (!long.TryParse(amountStr, out var vnpAmount))
        {
            return ("04", "Invalid amount");
        }

        decimal payableAmount = (decimal)vnpAmount / 100;
        if (transaction.Amount != payableAmount)
        {
            _logger.LogWarning("Amount mismatch for Ref {TxnRef}. DB: {DbAmount}, VNPay: {VnpAmount}", txnRef, transaction.Amount, payableAmount);
            return ("04", "Invalid amount");
        }

        // Check Transaction Status in DB (Idempotency)
        if (transaction.Status != "PENDING")
        {
            _logger.LogInformation("Duplicate transaction confirmation ignored for Ref {TxnRef}. Current Status: {Status}", txnRef, transaction.Status);
            return ("02", "Order already confirmed");
        }

        // Update status and process wallet balance credit
        if (responseCode == "00" && transactionStatus == "00")
        {
            using var dbTxn = await _context.Database.BeginTransactionAsync();
            try
            {
                transaction.Status = "SUCCESS";
                transaction.Description = $"{transaction.Description} | GD: {vnpTransactionNo} | Bank: {bankCode} | Paid: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss}";

                var wallet = await _context.Wallets.FindAsync(transaction.WalletId);
                if (wallet != null)
                {
                    decimal coinsEarned = transaction.Amount / 250m;
                    wallet.Balance += coinsEarned;
                    _context.Wallets.Update(wallet);
                }

                _context.Transactions.Update(transaction);
                await _context.SaveChangesAsync();
                await dbTxn.CommitAsync();

                _logger.LogInformation("Wallet credited successfully: User {UserId}, Amount {Amount} VND ({Coins} coins), Ref {TxnRef}", wallet?.UserId, transaction.Amount, transaction.Amount / 250m, txnRef);
            }
            catch (Exception ex)
            {
                await dbTxn.RollbackAsync();
                _logger.LogError(ex, "Error occurred during wallet deposit confirmation for Ref {TxnRef}", txnRef);
                throw;
            }
        }
        else
        {
            transaction.Status = "FAILED";
            transaction.Description = $"{transaction.Description} | Payer Cancelled/Failed | Code: {responseCode}";
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();

            _logger.LogWarning("Payment failed for Ref {TxnRef}. ResponseCode: {ResponseCode}", txnRef, responseCode);
        }

        return ("00", "Confirm Success");
    }

    [HttpGet("vnpay/{transactionReference}/status")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(string transactionReference)
    {
        var userId = GetCurrentUserId();
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "None";

        var transaction = await _context.Transactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == transactionReference);

        if (transaction == null)
        {
            return NotFound(new { message = "Transaction not found." });
        }

        // Authorization check: User can only check their own wallet transaction, unless Admin
        if (transaction.Wallet?.UserId != userId && userRole != "Admin")
        {
            return Forbid();
        }

        return Ok(new
        {
            transactionId = transaction.TransactionId,
            transactionReference = transaction.GatewayTransactionId,
            amount = transaction.Amount,
            status = transaction.Status,
            type = transaction.Type,
            description = transaction.Description,
            createdAt = transaction.CreatedAt
        });
    }
}

public class CreateDepositRequest
{
    public decimal Amount { get; set; }
    public string OrderInfo { get; set; } = "Nap tien vao vi tai khoan";
}
