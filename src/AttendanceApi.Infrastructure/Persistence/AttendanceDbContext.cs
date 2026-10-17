using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Submissions;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Infrastructure.Persistence;

public sealed class AttendanceDbContext : DbContext, IAttendanceDbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();

    public DbSet<SchoolAttendanceSettings> SchoolAttendanceSettings => Set<SchoolAttendanceSettings>();

    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();

    public DbSet<Term> Terms => Set<Term>();

    public DbSet<NonInstructionalDay> NonInstructionalDays => Set<NonInstructionalDay>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();

    public DbSet<AttendanceCode> AttendanceCodes => Set<AttendanceCode>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<StudentAttendanceSummary> StudentAttendanceSummaries => Set<StudentAttendanceSummary>();

    public DbSet<StudentAlert> StudentAlerts => Set<StudentAlert>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<AppUserSchool> AppUserSchools => Set<AppUserSchool>();

    public DbSet<AttendanceSubmission> AttendanceSubmissions => Set<AttendanceSubmission>();

    public DbSet<AttendanceSubmissionIssue> AttendanceSubmissionIssues => Set<AttendanceSubmissionIssue>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Legacy-to-new ID mapping used only by the legacy import tool, so it can run again safely.</summary>
    public DbSet<LegacyIdMap> LegacyIdMaps => Set<LegacyIdMap>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp is stored as UTC (Q2); mark values read back as UTC too.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<string>().AreUnicode();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);
}
