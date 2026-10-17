using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class StudentAlertConfiguration : IEntityTypeConfiguration<StudentAlert>
{
    public void Configure(EntityTypeBuilder<StudentAlert> builder)
    {
        builder.ToTable("StudentAlert");
        builder.Property(a => a.AlertType).HasMaxLength(StudentAlert.MaxAlertTypeLength).IsUnicode(false);
        builder.Property(a => a.SchoolYearLabel).HasMaxLength(9).IsUnicode(false);
        builder.Property(a => a.Message).HasMaxLength(StudentAlert.MaxMessageLength);
        builder.Property(a => a.ResolvedBy).HasMaxLength(Actor.MaxNameLength);

        // At most one open CHRONIC_ABSENCE alert per student per school year (BR-09).
        builder.HasIndex(a => new { a.StudentId, a.SchoolYearLabel })
            .IsUnique()
            .HasFilter($"[ResolvedAt] IS NULL AND [AlertType] = '{StudentAlert.ChronicAbsenceType}'")
            .HasDatabaseName("UX_StudentAlert_OpenChronicAbsence");
        builder.HasIndex(a => new { a.SchoolId, a.ResolvedAt });

        builder.HasOne<Student>().WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<School>().WithMany().HasForeignKey(a => a.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
