using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;

namespace AttendanceApi.Application.Chronic;

/// <summary>A student's chronic-absence position for one school year, combined across all schools.</summary>
public sealed record StudentChronicEvaluation(
    int StudentId,
    SchoolYearLabel SchoolYear,
    AttendanceTotals Totals,
    ChronicAbsenceResult Result,
    int? CurrentSchoolId,
    string? StatusReason,
    IReadOnlyList<int> SchoolIdsIncluded);
