using server.Endpoints;
using server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

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

// Page scores & cross-check
builder.Services.AddSingleton<PageScoreStore>();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Web Checker API");
    });
}

app.UseCors();

app.MapSecurityEndpoints();
app.MapAiEndpoints();
app.MapInfoEndpoints();

app.Run();
