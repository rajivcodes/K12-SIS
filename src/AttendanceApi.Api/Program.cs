using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AttendanceApi.Api.Infrastructure;
using AttendanceApi.Application;
using AttendanceApi.Infrastructure;
using AttendanceApi.Infrastructure.Auth;
using AttendanceApi.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

// Minimal console logger so failures before the host is built (bad configuration, missing secrets,
// database unreachable during migration) are still logged. Replaced by the configured logger below.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AttendanceApi"));

    builder.Services
        .AddControllers(o => o.Filters.Add<AppExceptionFilter>())
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    {
        if (ctx.HttpContext.Items[CorrelationIdMiddleware.HeaderName] is string correlationId)
        {
            ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    });
    builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Login attempts (failed and successful) are limited per client IP (plan §9).
    var loginLimit = builder.Configuration.GetSection(LoginRateLimitOptions.SectionName).Get<LoginRateLimitOptions>() ?? new LoginRateLimitOptions();
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(LoginRateLimitOptions.PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginLimit.PermitLimit,
                Window = TimeSpan.FromSeconds(loginLimit.WindowSeconds),
                QueueLimit = 0,
            }));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo { Title = "Attendance API", Version = "v1" });
        o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "AttendanceApi.Api.xml"));
        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the accessToken from POST /api/v1/auth/login.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
        };
        o.AddSecurityDefinition("Bearer", scheme);
        o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
    });

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diagnostics, http) =>
        diagnostics.Set("UserName", http.User.Identity?.Name ?? "anonymous"));
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    // Fail fast on missing secrets before touching the database (throws OptionsValidationException).
    _ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    await using (var scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by EF Core design-time tooling; anything else stopped the API.
    Log.Fatal(ex, "The API failed to start or stopped unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Exposed so the integration tests can host the API with WebApplicationFactory<Program>.
public partial class Program;
