using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Submissions;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AttendanceApi.Application.Abstractions;

/// <summary>
/// The use cases' view of the database. Queries are written in LINQ against these sets; the EF Core
/// implementation lives in Infrastructure.
/// </summary>
public interface IAttendanceDbContext
{
    DbSet<School> Schools { get; }

    DbSet<SchoolAttendanceSettings> SchoolAttendanceSettings { get; }

    DbSet<SchoolYear> SchoolYears { get; }

    DbSet<Term> Terms { get; }

    DbSet<NonInstructionalDay> NonInstructionalDays { get; }

    DbSet<Student> Students { get; }

    DbSet<StudentEnrollment> StudentEnrollments { get; }

    DbSet<AttendanceCode> AttendanceCodes { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<StudentAttendanceSummary> StudentAttendanceSummaries { get; }

    DbSet<StudentAlert> StudentAlerts { get; }

    DbSet<AppUser> AppUsers { get; }

    DbSet<AppUserSchool> AppUserSchools { get; }

    DbSet<AttendanceSubmission> AttendanceSubmissions { get; }

    DbSet<AttendanceSubmissionIssue> AttendanceSubmissionIssues { get; }

    /// <summary>Used by the save flow to run one database transaction per save.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
