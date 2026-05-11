using System.Text;
using MedicalFeaturePrototype.Api.Data;
using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection("SeedAdmin"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (!string.IsNullOrWhiteSpace(jwtOptions.Secret))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
}

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IExcelDataService, ExcelDataService>();
builder.Services.AddScoped<IPatientTextService, PatientTextService>();
builder.Services.AddScoped<IFeatureAnalysisService, FeatureAnalysisService>();
builder.Services.AddScoped<IPromptComposerService, PromptComposerService>();
builder.Services.AddScoped<IPromptExecutionService, PromptExecutionService>();
builder.Services.AddScoped<IPromptVotingService, PromptVotingService>();
builder.Services.AddScoped<IMedicalTextWorkflowService, MedicalTextWorkflowService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProcessingRequestLogService, ProcessingRequestLogService>();
builder.Services.AddScoped<AdminBootstrapService>();

builder.Services.Configure<ChatGptOptions>(builder.Configuration.GetSection("LlmProviders:ChatGpt"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("LlmProviders:Gemini"));
builder.Services.Configure<GigaChatOptions>(builder.Configuration.GetSection("LlmProviders:GigaChat"));

builder.Services.AddHttpClient<IChatGptApiService, ChatGptApiService>();
builder.Services.AddHttpClient<IGeminiApiService, GeminiApiService>();
builder.Services.AddHttpClient<IGigaChatTokenService, GigaChatTokenService>();
builder.Services.AddHttpClient<IGigaChatApiService, GigaChatApiService>();
builder.Services.AddHttpClient<IKnowledgeBaseSolverService, KnowledgeBaseSolverService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var adminBootstrapService = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();
    await adminBootstrapService.SeedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    try
    {
        await next();
    }
    catch (Exception exception)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        var isExternalServiceError = exception is HttpRequestException or TaskCanceledException;
        var statusCode = isExternalServiceError
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        var message = isExternalServiceError
            ? "Не удалось связаться с внешним сервисом. Проверьте подключение к интернету и повторите запрос."
            : "Во время обработки запроса произошла ошибка. Повторите попытку позже.";

        app.Logger.LogError(
            exception,
            "[SERVER] API request failed {Method} {Path} -> {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            statusCode);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            message,
            status = statusCode
        });
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var logger = app.Logger;
    var requestId = Guid.NewGuid().ToString("N")[..8];

    logger.LogInformation(
        "[SERVER] Request received {RequestId} {Method} {Path}{QueryString}",
        requestId,
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);

    await next();

    logger.LogInformation(
        "[SERVER] Response sent {RequestId} {Method} {Path} -> {StatusCode}",
        requestId,
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode);
});

app.MapControllers();

app.Run();
