using AttendanceApi.Application.Admin;
using AttendanceApi.Application.Attendance;
using AttendanceApi.Application.Auth;
using AttendanceApi.Application.Chronic;
using AttendanceApi.Application.Codes;
using AttendanceApi.Application.Common;
using AttendanceApi.Application.Students;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessGuard>();
        services.AddScoped<ChronicStatusCalculator>();
        services.AddScoped<SaveDailyAttendanceHandler>();
        services.AddScoped<AttendanceHistoryQuery>();
        services.AddScoped<ChronicStatusQuery>();
        services.AddScoped<RosterQuery>();
        services.AddScoped<AttendanceCodesQuery>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<AuditLog>();
        services.AddScoped<SchoolSettingsAdmin>();
        services.AddScoped<SchoolCalendarAdmin>();
        services.AddScoped<SubmissionIssuesQuery>();
        services.AddScoped<UsersAdmin>();
        return services;
    }
}
