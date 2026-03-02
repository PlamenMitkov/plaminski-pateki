using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<OpenRouteServiceOptions>(builder.Configuration.GetSection("OpenRouteService"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHttpClient<OpenRouteService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddHttpClient<IOpenAiAssistantService, OpenAiAssistantService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddHttpClient<IVectorService, OpenAiVectorService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();
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
    throw new InvalidOperationException("JWT key must be configured and at least 32 characters long.");
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
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    await dbContext.Database.MigrateAsync();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthorizationFilter()]
});
app.MapControllers();

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
