using Microsoft.EntityFrameworkCore;
using Serilog;
using server.Checks.Ai;
using server.Checks.Security;
using server.Data;
using server.Endpoints;
using server.Middleware;
using server.Models;
using server.Services;
using Microsoft.Extensions.Options;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    var builder = WebApplication.CreateBuilder(args);

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
    builder.Host.UseSerilog(Log.Logger);

    builder.Services.AddOpenApi();
    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient("NoRedirect")
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Services
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
    builder.Services.AddTransient<ISecurityCheck, SecurityHeadersCheck>();
    builder.Services.AddTransient<ISecurityCheck, RedirectChainCheck>();
    builder.Services.AddTransient<ISecurityCheck, MixedContentCheck>();
    builder.Services.AddTransient<SecurityCheckService>();

    // Register AI checks
    builder.Services.AddTransient<IAiCheck, VocabularyRichnessCheck>();
    builder.Services.AddTransient<IAiCheck, SentenceUniformityCheck>();
    builder.Services.AddTransient<IAiCheck, PerplexityEstimationCheck>();
    builder.Services.AddTransient<IAiCheck, PunctuationPatternsCheck>();
    builder.Services.AddTransient<IAiCheck, RepetitivePhrasingCheck>();
    builder.Services.AddTransient<IAiCheck, ParagraphStructureCheck>();
    builder.Services.AddTransient<IAiCheck, TransitionalPhraseCheck>();
    builder.Services.AddTransient<IAiCheck, HedgingLanguageCheck>();
    builder.Services.AddTransient<IAiCheck, ClaudeAiModelCheck>();
    builder.Services.AddTransient<AiCheckService>();

    // Clients
    builder.Services.AddSingleton<server.Helpers.HtmlTextExtractor>();
    builder.Services.AddTransient<server.Clients.AnthropicClient>();
    builder.Services.AddTransient<server.Clients.BraveSearchClient>();

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

    Log.Information("Starting ClearSource application...");

    // Apply database migrations automatically (skip for InMemory testing)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            Log.Information("Applying database migrations...");
            db.Database.Migrate();
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "ClearSource API");
        });
        Log.Information("Swagger UI enabled for development");
    }

    // Add request logging middleware
    app.UseRequestLogging();

    app.UseCors();

    app.MapSecurityEndpoints();
    app.MapAiEndpoints();
    app.MapInfoEndpoints();

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

// Make the implicit Program class public for WebApplicationFactory in tests
public partial class Program { }
