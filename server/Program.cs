using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using server.Data;
using server.Endpoints;
using server.Middleware;
using server.Models;
using server.Services;

// Configure Serilog logging
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog as the logging provider
    builder.Host.UseSerilog(Log.Logger);

    builder.Services.AddOpenApi();
    builder.Services.AddHttpClient();

// Database context - NeonDB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication & Authorization
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JWT Secret is not configured or too short. Set Jwt:Secret in appsettings.json");
}

var key = Encoding.UTF8.GetBytes(jwtSecret);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "YourAppName",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "YourAppUsers",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<PageScoreStore>();

// Phishing brand configuration from appsettings
builder.Services.Configure<PhishingSettings>(builder.Configuration.GetSection("Phishing"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PhishingSettings>>().Value);

// Register security checks
builder.Services.AddTransient<ISecurityCheck, HttpsCheck>();
builder.Services.AddTransient<ISecurityCheck, DomainAgeCheck>();
builder.Services.AddTransient<ISecurityCheck, SuspiciousLinksCheck>();
builder.Services.AddTransient<ISecurityCheck, SslCertificateCheck>();
builder.Services.AddTransient<ISecurityCheck, PhishingCheck>();
builder.Services.AddTransient<ISecurityCheck, GoogleSafeBrowsingCheck>();
builder.Services.AddTransient<SecurityCheckService>();

// Register AI checks
builder.Services.AddTransient<IAiCheck, VocabularyRichnessCheck>();
builder.Services.AddTransient<IAiCheck, SentenceUniformityCheck>();
builder.Services.AddTransient<IAiCheck, PerplexityEstimationCheck>();
builder.Services.AddTransient<IAiCheck, PunctuationPatternsCheck>();
builder.Services.AddTransient<IAiCheck, RepetitivePhrasingCheck>();
builder.Services.AddTransient<IAiCheck, ParagraphStructureCheck>();
builder.Services.AddTransient<IAiCheck, ClaudeAiModelCheck>();
builder.Services.AddTransient<AiCheckService>();

// Cross-check
builder.Services.AddTransient<CrossCheckService>();

// CORS for browser extension
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

    Log.Information("Starting Web Checker application...");

    // Apply database migrations automatically
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("Applying database migrations...");
        db.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Web Checker API");
        });
        Log.Information("Swagger UI enabled for development");
    }

    // Add request logging middleware (must be early in pipeline)
    app.UseRequestLogging();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapSecurityEndpoints();
    app.MapAiEndpoints();
    app.MapInfoEndpoints();
    app.MapUserEndpoints();
    app.MapPaymentEndpoints();

    Log.Information("Application started successfully on {Date}", DateTime.UtcNow);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutdown at {Date}", DateTime.UtcNow);
    await Log.CloseAndFlushAsync();
}
