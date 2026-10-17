using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using AttendanceApi.Infrastructure.Persistence;
using AttendanceApi.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceApi.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "AttendanceDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));

        // The connection string is read when the context is first created, so test hosts can override it.
        services.AddDbContext<AttendanceDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} is not configured. Set it with user-secrets or an environment variable.");
            }

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });
        services.AddScoped<IAttendanceDbContext>(sp => sp.GetRequiredService<AttendanceDbContext>());

        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<DatabaseInitializer>();

        services.AddHealthChecks().AddDbContextCheck<AttendanceDbContext>("database");

        return services;
    }
}
