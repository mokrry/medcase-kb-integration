using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services;
using MedicalFeaturePrototype.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
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

builder.Services.Configure<ChatGptOptions>(builder.Configuration.GetSection("LlmProviders:ChatGpt"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("LlmProviders:Gemini"));
builder.Services.Configure<GigaChatOptions>(builder.Configuration.GetSection("LlmProviders:GigaChat"));

builder.Services.AddHttpClient<IChatGptApiService, ChatGptApiService>();
builder.Services.AddHttpClient<IGeminiApiService, GeminiApiService>();
builder.Services.AddHttpClient<IGigaChatTokenService, GigaChatTokenService>();
builder.Services.AddHttpClient<IGigaChatApiService, GigaChatApiService>();

var app = builder.Build();

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
