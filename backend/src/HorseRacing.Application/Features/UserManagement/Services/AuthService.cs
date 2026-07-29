using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Common.Interfaces;
using HorseRacing.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace HorseRacing.Application.Features.UserManagement.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IGoogleTokenVerifier _googleTokenVerifier;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator, IGoogleTokenVerifier googleTokenVerifier, IEmailService emailService, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _googleTokenVerifier = googleTokenVerifier;
        _emailService = emailService;
        _configuration = configuration;
        _passwordHasher = new PasswordHasher<AppUser>();
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (!user.IsEmailConfirmed)
        {
            throw new UnauthorizedAccessException("Account is not activated. Please check your email for verification.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            Message = "Login successful",
            Result = new AuthResult
            {
                AccessToken = token,
                RefreshToken = null,
                User = new UserDto
                {
                    Id = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role?.Name ?? "Spectator",
                    Status = user.Status
                }
            }
        };
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters.");
        }
        if (request.Password != request.ConfirmPassword)
        {
            throw new ArgumentException("Confirm password does not match.");
        }

        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new ArgumentException("Email already exists.");
        }

        var verificationToken = Guid.NewGuid().ToString();
        var tokenExpiresAt = DateTime.UtcNow.AddMinutes(15);

        var newUser = new AppUser
        {
            Username = request.Email.Split('@')[0],
            Email = request.Email,
            FullName = request.FullName,
            RoleId = 5, // Spectator role
            IsEmailConfirmed = false,
            VerificationToken = verificationToken,
            TokenExpiresAt = tokenExpiresAt
        };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);

        await _userRepository.AddAsync(newUser);
        await _userRepository.SaveChangesAsync();

        try
        {
            var baseUrl = _configuration["BaseUrl"]?.TrimEnd('/') ?? "https://localhost:55445";
            var verificationLink = $"{baseUrl}/api/auth/verify-email?email={Uri.EscapeDataString(newUser.Email)}&token={Uri.EscapeDataString(verificationToken)}";
            var htmlBody = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;"">
    <h2 style=""color: #333; text-align: center;"">Account Verification</h2>
    <p>Hello <strong>{newUser.FullName}</strong>,</p>
    <p>Thank you for registering an account on the Horse Racing Management System.</p>
    <p>Please click the link below to verify and activate your account (link is valid for 15 minutes):</p>
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{verificationLink}"" style=""background-color: #4CAF50; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold;"">Verify Email</a>
    </div>
    <p>If the button above does not work, you can also copy the link below into your browser:</p>
    <p style=""word-break: break-all;""><a href=""{verificationLink}"">{verificationLink}</a></p>
    <hr style=""border: 0; border-top: 1px solid #eee; margin: 20px 0;"" />
    <p style=""font-size: 12px; color: #777; text-align: center;"">This is an automated email from the system. Please do not reply.</p>
</div>";
            await _emailService.SendEmailAsync(newUser.Email, "Horse Racing Management System Account Verification", htmlBody);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Registration successful but verification email could not be sent. SMTP Error: {ex.Message}", ex);
        }

        return new AuthResponse
        {
            Message = "Registration successful. Please check your email to verify and activate your account.",
            Result = new AuthResult
            {
                AccessToken = string.Empty,
                RefreshToken = null,
                User = new UserDto
                {
                    Id = newUser.UserId,
                    FullName = newUser.FullName,
                    Email = newUser.Email,
                    Role = "Spectator",
                    Status = newUser.Status
                }
            }
        };
    }

    public async Task<AuthResponse?> GoogleLoginAsync(GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new ArgumentException("IdToken is required.");
        }

        var googleUser = await _googleTokenVerifier.VerifyTokenAsync(request.IdToken);
        if (googleUser == null)
        {
            return null;
        }

        var email = googleUser.Email;
        var existingUser = await _userRepository.GetByEmailAsync(email);

        AppUser user;
        if (existingUser != null)
        {
            var roleName = existingUser.Role?.Name ?? string.Empty;
            if (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                roleName.Equals("Referee", StringComparison.OrdinalIgnoreCase) || 
                roleName.Equals("RaceReferee", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("System administrator accounts are not allowed to be linked automatically via Google Login.");
            }

            user = existingUser;
        }
        else
        {
            user = new AppUser
            {
                Username = email.Split('@')[0],
                Email = email,
                FullName = string.IsNullOrWhiteSpace(googleUser.Name) ? email.Split('@')[0] : googleUser.Name,
                RoleId = 5, // Spectator role
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                PasswordHash = string.Empty
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            // Fetch user with role information populated
            user = await _userRepository.GetByEmailAsync(email) ?? user;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            Message = "Google login successful",
            Result = new AuthResult
            {
                AccessToken = token,
                RefreshToken = null,
                User = new UserDto
                {
                    Id = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role?.Name ?? "Spectator",
                    Status = user.Status
                }
            }
        };
    }

    public async Task<bool> VerifyEmailAsync(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        if (user.VerificationToken != token)
        {
            return false;
        }

        if (user.TokenExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        user.IsEmailConfirmed = true;
        user.VerificationToken = null;
        user.TokenExpiresAt = null;

        await _userRepository.SaveChangesAsync();
        return true;
    }
}