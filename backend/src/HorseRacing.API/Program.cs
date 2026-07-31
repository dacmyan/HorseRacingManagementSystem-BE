using System.Text;
using HorseRacing.API.Extensions;
using HorseRacing.API.Middlewares;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// 1. CẤU HÌNH CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://horse-tournament-management.vercel.app", 
                "http://localhost:5173", 
                "http://localhost:5174",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:5174"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); 
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerExtensions();

// 2. CẤU HÌNH AUTHENTICATION (Kèm xử lý Token cho WebSockets/SignalR)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is not configured.")))
    };
    
    // Rất quan trọng để FE gọi được Hub Notification
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddServiceExtensions(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<HorseRacing.API.Services.TournamentDeadlineWorker>();

var app = builder.Build();

// 3. THỨ TỰ MIDDLEWARE (BẮT BUỘC PHẢI CHUẨN)
app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Bắt đầu luồng xử lý Request
app.UseRouting();

// Mở cửa CORS ngay sau Routing
app.UseCors("CorsPolicy"); 

// Xác thực danh tính sau khi đã qua cửa CORS
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<HorseRacing.API.Hubs.NotificationHub>("/hubs/notification");

// 4. MIGRATION & SEEDING (Tự động cập nhật Database)
// using (var scope = app.Services.CreateScope())
// {
//     var services = scope.ServiceProvider;
//     var logger = services.GetRequiredService<ILogger<Program>>();
//     try
//     {
//         logger.LogInformation("Checking database migrations...");
//         var db = services.GetRequiredService<AppDbContext>();
//         // await db.Database.MigrateAsync();
//         logger.LogInformation("Database migration check skipped.");

//         // var dataSeeder = services.GetRequiredService<HorseRacing.Infrastructure.Persistence.DataSeeder>();
//         // await dataSeeder.SeedAsync();
//     }
//     catch (Exception ex)
//     {
//         logger.LogError(ex, "An error occurred during database migration or seeding. Continuing startup...");
//     }
// }

app.Run();