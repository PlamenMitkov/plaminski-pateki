using EcoTrails.Api.Data;
using EcoTrails.Api.Middleware;
using EcoTrails.Api.Models;
using EcoTrails.Api.OpenApi;
using EcoTrails.Api.Repositories;
using EcoTrails.Api.Services;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Security.Claims;
using System.Globalization;
using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var isTesting = builder.Environment.IsEnvironment("Testing");
var disableBackgroundServices = isTesting || builder.Configuration.GetValue<bool>("Testing:DisableBackgroundServices");
var skipDataInitialization = isTesting || builder.Configuration.GetValue<bool>("Testing:SkipDataInitialization");
var telemetryServiceName = builder.Configuration["OpenTelemetry:ServiceName"];
if (string.IsNullOrWhiteSpace(telemetryServiceName))
{
    telemetryServiceName = "EcoTrails.Api";
}

var telemetryEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"];
var hasTelemetryExporter = Uri.TryCreate(telemetryEndpoint, UriKind.Absolute, out var telemetryEndpointUri);
var telemetryProtocolValue = builder.Configuration["OpenTelemetry:Otlp:Protocol"];
var telemetryProtocol = string.Equals(telemetryProtocolValue, "http/protobuf", StringComparison.OrdinalIgnoreCase)
    ? OtlpExportProtocol.HttpProtobuf
    : OtlpExportProtocol.Grpc;

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
if (isTesting)
{
    var testDatabaseName = builder.Configuration.GetValue<string>("Testing:DatabaseName") ?? "EcoTrailsTesting";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase(testDatabaseName));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));
}
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<OpenRouteServiceOptions>(builder.Configuration.GetSection("OpenRouteService"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AdminPanelOptions>(builder.Configuration.GetSection("AdminPanel"));
builder.Services.AddScoped<ITrailProposalReviewService, TrailProposalReviewService>();
builder.Services.AddScoped<ITrailRepository, TrailRepository>();
builder.Services.AddScoped<IFavoritesRepository, FavoritesRepository>();
builder.Services.AddScoped<IAssistantSessionReadRepository, AssistantSessionReadRepository>();
builder.Services.AddScoped<IAssistantSessionWriteRepository, AssistantSessionWriteRepository>();
builder.Services.AddScoped<IAssistantMessageRepository, AssistantMessageRepository>();
builder.Services.AddScoped<IAssistantSessionOrchestrationService, AssistantSessionOrchestrationService>();
builder.Services.AddScoped<IAssistantPromptSafetyService, AssistantPromptSafetyService>();
builder.Services.AddScoped<IAssistantPromptAssemblyService, AssistantPromptAssemblyService>();
builder.Services.AddScoped<IAssistantProvenancePolicyService, AssistantProvenancePolicyService>();
builder.Services.AddScoped<IAssistantRetrievalService, AssistantRetrievalService>();
builder.Services.AddScoped<IAssistantEnrichmentWorkflowService, AssistantEnrichmentWorkflowService>();
builder.Services.AddScoped<IAssistantResponseCompositionService, AssistantResponseCompositionService>();
builder.Services.AddScoped<ITrailOfflineEnrichmentService, TrailOfflineEnrichmentService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHttpClient<OpenRouteService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(12);
})
    .AddHttpMessageHandler(serviceProvider =>
        new OutboundHttpMetricsHandler(serviceProvider.GetRequiredService<IMeterFactory>(), "openroute"));
builder.Services.AddHttpClient<IOpenAiProvider, OpenAiProvider>(httpClient =>
{
    var options = builder.Configuration.GetSection("OpenAI").Get<OpenAiOptions>();
    var baseUrl = options?.BaseUrl ?? "https://api.openai.com/v1/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    httpClient.BaseAddress = new Uri(baseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(25);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IGeminiProvider, GeminiProvider>(httpClient =>
{
    var options = builder.Configuration.GetSection("OpenAI").Get<OpenAiOptions>();
    var geminiBaseUrl = options?.GeminiBaseUrl ?? "https://generativelanguage.googleapis.com/v1beta/";
    if (!geminiBaseUrl.EndsWith('/'))
    {
        geminiBaseUrl += "/";
    }

    httpClient.BaseAddress = new Uri(geminiBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(25);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IAiProviderClient, AiProviderClient>();
builder.Services.AddScoped<IOpenAiAssistantService, AssistantService>();
builder.Services.AddScoped<IAiProviderFallbackPolicy, AiProviderFallbackPolicy>();
builder.Services.AddHttpClient<IAssistantWeatherContextService, AssistantWeatherContextService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(12);
})
.AddHttpMessageHandler(serviceProvider =>
    new OutboundHttpMetricsHandler(serviceProvider.GetRequiredService<IMeterFactory>(), "assistant-weather"));
builder.Services.AddHttpClient<IVectorService, OpenAiVectorService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(20);
})
    .AddHttpMessageHandler(serviceProvider =>
        new OutboundHttpMetricsHandler(serviceProvider.GetRequiredService<IMeterFactory>(), "openai-vector"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder =>
    {
        resourceBuilder.AddService(serviceName: telemetryServiceName);
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = false;
                options.SetDbStatementForStoredProcedure = false;
            });

        if (hasTelemetryExporter && telemetryEndpointUri is not null)
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = telemetryEndpointUri;
                options.Protocol = telemetryProtocol;
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(OutboundHttpMetricsHandler.MeterName);

        if (hasTelemetryExporter && telemetryEndpointUri is not null)
        {
            metrics.AddOtlpExporter(options =>
            {
                options.Endpoint = telemetryEndpointUri;
                options.Protocol = telemetryProtocol;
            });
        }
    });
if (!disableBackgroundServices)
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddHangfireServer();
}
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) ||
    jwtOptions.Key.Contains("changethis", StringComparison.OrdinalIgnoreCase) ||
    jwtOptions.Key.Length < 32)
{
    if (isTesting)
    {
        jwtOptions.Key = "testing-jwt-key-please-change-1234567890";
    }
    else
    {
        throw new InvalidOperationException("JWT key must be configured and at least 32 characters long.");
    }
}

var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = jwtKey,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsync("Too many requests.", cancellationToken);
    };

    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    options.AddTokenBucketLimiter("assistant", limiterOptions =>
    {
        limiterOptions.TokenLimit = 30;
        limiterOptions.TokensPerPeriod = 30;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        limiterOptions.AutoReplenishment = true;
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("assistant-enrich", limiterOptions =>
    {
        limiterOptions.PermitLimit = 3;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.AddScoped<EcoJsonImportService>();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>(name: "database", tags: ["ready"]);
builder.Services.AddControllers();
var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var corsOrigins = configuredCorsOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

corsOrigins = corsOrigins.Length > 0
    ? corsOrigins
    : builder.Environment.IsDevelopment()
        ? ["http://localhost:5173", "http://127.0.0.1:5173"]
        : [];

var corsMethods = builder.Configuration.GetSection("Cors:AllowedMethods").Get<string[]>() ?? ["GET", "POST", "PUT", "DELETE", "OPTIONS"];
var corsHeaders = builder.Configuration.GetSection("Cors:AllowedHeaders").Get<string[]>() ?? ["Authorization", "Content-Type", "Accept"];

if (corsOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS allowed origins must be configured outside development.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        policyBuilder => policyBuilder
            .WithOrigins(corsOrigins)
            .WithMethods(corsMethods)
            .WithHeaders(corsHeaders)
            .WithExposedHeaders("X-Total-Count"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT token in format: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme.ToLowerInvariant(),
        BearerFormat = "JWT"
    });

    options.OperationFilter<AuthAndRateLimitOperationFilter>();
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (!skipDataInitialization)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        const string adminRoleName = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRoleName));
        }

        var adminEmails = builder.Configuration.GetSection("Admin:Emails").Get<string[]>() ?? [];
        foreach (var email in adminEmails)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var normalizedEmail = email.Trim();
            var user = await userManager.FindByEmailAsync(normalizedEmail);
            if (user is null)
            {
                continue;
            }

            if (!await userManager.IsInRoleAsync(user, adminRoleName))
            {
                await userManager.AddToRoleAsync(user, adminRoleName);
            }
        }

        var importer = scope.ServiceProvider.GetRequiredService<EcoJsonImportService>();
        await importer.ImportFromEcoJsonAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.ToString());

        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            diagnosticContext.Set("UserId", userId);
        }
    };
});
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowReact");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (!disableBackgroundServices)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAdminAuthorizationFilter()]
    });

    RecurringJob.AddOrUpdate<ITrailOfflineEnrichmentService>(
        "trail-offline-enrichment-daily",
        service => service.WarmDailyCacheAsync(),
        Cron.Daily);
}
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program;
