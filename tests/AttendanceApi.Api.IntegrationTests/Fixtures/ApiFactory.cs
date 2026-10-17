using AttendanceApi.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AttendanceApi.Api.IntegrationTests.Fixtures;

/// <summary>Hosts the real API against a test database, with a fixed clock and test-only secrets.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Admin-Test-Pw-1";

    private readonly string _connectionString;
    private readonly int _loginPermitLimit;
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiFactory(string connectionString, int loginPermitLimit = 1000, Action<IServiceCollection>? configureServices = null)
    {
        _connectionString = connectionString;
        _loginPermitLimit = loginPermitLimit;
        _configureServices = configureServices;
    }

    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AttendanceDb", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(Enumerable.Range(1, 48).Select(i => (byte)i).ToArray()));
        builder.UseSetting("SeedAdmin:UserName", AdminUserName);
        builder.UseSetting("SeedAdmin:Password", AdminPassword);
        builder.UseSetting("Auth:LoginRateLimit:PermitLimit", _loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            _configureServices?.Invoke(services);
        });
    }
}
