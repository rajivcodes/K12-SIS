using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.LegacyImport.Legacy;

/// <summary>
/// Read-only EF Core view of the legacy database, so the import reads it with LINQ like the rest of the
/// solution. Never saved: queries use no tracking, and <see cref="SaveChanges()"/> is blocked.
/// </summary>
public sealed class LegacyDbContext : DbContext
{
    public LegacyDbContext(DbContextOptions<LegacyDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<LegacySchool> Schools => Set<LegacySchool>();

    public DbSet<LegacySchoolTerm> SchoolTerms => Set<LegacySchoolTerm>();

    public DbSet<LegacyStudent> Students => Set<LegacyStudent>();

    public DbSet<LegacyAttendanceCode> AttendanceCodes => Set<LegacyAttendanceCode>();

    public DbSet<LegacyStudentAttendance> StudentAttendance => Set<LegacyStudentAttendance>();

    public DbSet<LegacyStudentAlert> StudentAlerts => Set<LegacyStudentAlert>();

    public override int SaveChanges() => throw new InvalidOperationException("The legacy database is read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The legacy database is read-only.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LegacySchool>(e =>
        {
            e.ToTable("Schools");
            e.HasKey(x => x.SchoolId);
            e.Property(x => x.SchoolId).HasColumnName("SchoolID");
        });
        modelBuilder.Entity<LegacySchoolTerm>(e =>
        {
            e.ToTable("SchoolTerms");
            e.HasKey(x => x.TermId);
            e.Property(x => x.TermId).HasColumnName("TermID");
            e.Property(x => x.SchoolId).HasColumnName("SchoolID");
        });
        modelBuilder.Entity<LegacyStudent>(e =>
        {
            e.ToTable("Students");
            e.HasKey(x => x.StudentId);
            e.Property(x => x.StudentId).HasColumnName("StudentID");
            e.Property(x => x.SchoolId).HasColumnName("SchoolID");
        });
        modelBuilder.Entity<LegacyAttendanceCode>(e =>
        {
            e.ToTable("AttendanceCodes");
            e.HasKey(x => x.CodeValue);
        });
        modelBuilder.Entity<LegacyStudentAttendance>(e =>
        {
            e.ToTable("StudentAttendance");
            e.HasKey(x => x.AttendanceId);
            e.Property(x => x.AttendanceId).HasColumnName("AttendanceID");
            e.Property(x => x.StudentId).HasColumnName("StudentID");
            e.Property(x => x.SchoolId).HasColumnName("SchoolID");
            e.Property(x => x.TermId).HasColumnName("TermID");
        });
        modelBuilder.Entity<LegacyStudentAlert>(e =>
        {
            e.ToTable("StudentAlerts");
            e.HasKey(x => x.AlertId);
            e.Property(x => x.AlertId).HasColumnName("AlertID");
            e.Property(x => x.StudentId).HasColumnName("StudentID");
            e.Property(x => x.SchoolId).HasColumnName("SchoolID");
        });
    }
}
