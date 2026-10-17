using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Schools;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Admin;

public sealed record SchoolSettingsResponse(
    int SchoolId,
    ChronicMethod ChronicMethod,
    int ChronicCountThreshold,
    decimal ChronicRateThreshold,
    bool AllowFutureDates,
    bool AllowWeekends,
    bool AllowOutsideTerm,
    int? MaxBackdateDays,
    int? MaxFutureDays);

/// <summary>Full replacement of a school's settings. Omitted limits mean "unlimited".</summary>
public sealed record UpdateSchoolSettingsRequest(
    ChronicMethod? ChronicMethod,
    int? ChronicCountThreshold,
    decimal? ChronicRateThreshold,
    bool? AllowFutureDates,
    bool? AllowWeekends,
    bool? AllowOutsideTerm,
    int? MaxBackdateDays,
    int? MaxFutureDays);

/// <summary>Chronic thresholds and date rules per school (plan decisions 4 and 10, Q4).</summary>
public sealed class SchoolSettingsAdmin
{
    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly AuditLog _audit;

    public SchoolSettingsAdmin(IAttendanceDbContext db, AccessGuard access, AuditLog audit)
    {
        _db = db;
        _access = access;
        _audit = audit;
    }

    public async Task<SchoolSettingsResponse> GetAsync(int schoolId, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        await AdminLookups.EnsureSchoolExistsAsync(_db, schoolId, cancellationToken);
        var settings = await _db.SchoolAttendanceSettings.AsNoTracking().SingleOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken)
            ?? SchoolAttendanceSettings.CreateDefault(schoolId);
        return ToResponse(settings);
    }

    public async Task<SchoolSettingsResponse> UpdateAsync(int schoolId, UpdateSchoolSettingsRequest request, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        await AdminLookups.EnsureSchoolExistsAsync(_db, schoolId, cancellationToken);
        if (request.ChronicMethod is not { } method || request.ChronicCountThreshold is not { } count || request.ChronicRateThreshold is not { } rate
            || request.AllowFutureDates is not { } future || request.AllowWeekends is not { } weekends || request.AllowOutsideTerm is not { } outsideTerm)
        {
            throw new RequestValidationException(
                "chronicMethod, chronicCountThreshold, chronicRateThreshold, allowFutureDates, allowWeekends and allowOutsideTerm are required.");
        }

        var settings = await _db.SchoolAttendanceSettings.SingleOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken);
        if (settings is null)
        {
            settings = SchoolAttendanceSettings.CreateDefault(schoolId);
            _db.SchoolAttendanceSettings.Add(settings);
        }

        settings.SetChronicRules(method, count, rate);
        settings.SetDateRules(future, weekends, outsideTerm, request.MaxBackdateDays, request.MaxFutureDays);
        var response = ToResponse(settings);
        _audit.Add("SchoolSettingsUpdated", "SchoolAttendanceSettings", schoolId, response);
        await _db.SaveChangesAsync(cancellationToken);
        return response;
    }

    private static SchoolSettingsResponse ToResponse(SchoolAttendanceSettings s) => new(
        s.SchoolId, s.ChronicMethod, s.ChronicCountThreshold, s.ChronicRateThreshold,
        s.AllowFutureDates, s.AllowWeekends, s.AllowOutsideTerm, s.MaxBackdateDays, s.MaxFutureDays);
}
