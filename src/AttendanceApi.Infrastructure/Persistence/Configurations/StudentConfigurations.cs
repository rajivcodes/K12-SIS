using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Student");
        builder.Property(s => s.FirstName).HasMaxLength(Student.MaxNameLength);
        builder.Property(s => s.LastName).HasMaxLength(Student.MaxNameLength);
        builder.Property(s => s.Grade).HasMaxLength(Student.MaxGradeLength);
        builder.HasIndex(s => new { s.LastName, s.FirstName });
    }
}

internal sealed class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("StudentEnrollment", t =>
            t.HasCheckConstraint("CK_StudentEnrollment_Dates", "[ExitDate] IS NULL OR [ExitDate] >= [EntryDate]"));
        builder.HasIndex(e => new { e.StudentId, e.EntryDate });
        builder.HasIndex(e => new { e.SchoolId, e.EntryDate });
        builder.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<School>().WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
