using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceCodeConfiguration : IEntityTypeConfiguration<AttendanceCode>
{
    public void Configure(EntityTypeBuilder<AttendanceCode> builder)
    {
        builder.ToTable("AttendanceCode");
        builder.Property(c => c.Code).HasMaxLength(AttendanceCode.MaxCodeLength);
        builder.Property(c => c.Description).HasMaxLength(AttendanceCode.MaxDescriptionLength);
        builder.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecord", t =>
            t.HasCheckConstraint("CK_AttendanceRecord_MinutesLate", "[MinutesLate] >= 0"));

        // One record per student per day (fixes D-06).
        builder.HasIndex(r => new { r.StudentId, r.Date }).IsUnique();
        builder.HasIndex(r => new { r.SchoolId, r.Date });
        builder.HasIndex(r => new { r.SchoolYearId, r.StudentId });

        builder.Property(r => r.Notes).HasMaxLength(AttendanceRecord.MaxNotesLength);
        builder.Property(r => r.CreatedBy).HasMaxLength(Actor.MaxNameLength);
        builder.Property(r => r.ModifiedBy).HasMaxLength(Actor.MaxNameLength);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<School>().WithMany().HasForeignKey(r => r.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SchoolYear>().WithMany().HasForeignKey(r => r.SchoolYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Term>().WithMany().HasForeignKey(r => r.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttendanceCode>().WithMany().HasForeignKey(r => r.AttendanceCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.ModifiedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentAttendanceSummaryConfiguration : IEntityTypeConfiguration<StudentAttendanceSummary>
{
    public void Configure(EntityTypeBuilder<StudentAttendanceSummary> builder)
    {
        builder.ToTable("StudentAttendanceSummary", t =>
        {
            t.HasCheckConstraint("CK_StudentAttendanceSummary_Counts", "[ExcusedAbsences] >= 0 AND [UnexcusedAbsences] >= 0");
            t.HasCheckConstraint("CK_StudentAttendanceSummary_Total", "[TotalAbsences] = [ExcusedAbsences] + [UnexcusedAbsences]");
        });

        // District-wide per student and school year (fixes D-08).
        builder.HasKey(s => new { s.StudentId, s.SchoolYearLabel });
        builder.Property(s => s.SchoolYearLabel).HasMaxLength(9).IsUnicode(false);
        builder.HasOne<Student>().WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
