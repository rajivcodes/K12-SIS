using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Domain.Submissions;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceSubmissionConfiguration : IEntityTypeConfiguration<AttendanceSubmission>
{
    public void Configure(EntityTypeBuilder<AttendanceSubmission> builder)
    {
        builder.ToTable("AttendanceSubmission", t =>
            t.HasCheckConstraint(
                "CK_AttendanceSubmission_Rows",
                "[RowsSaved] >= 0 AND [RowsSkipped] >= 0 AND [RowsReceived] = [RowsSaved] + [RowsSkipped]"));
        builder.Property(s => s.SubmittedBy).HasMaxLength(Actor.MaxNameLength);
        builder.HasIndex(s => new { s.SchoolId, s.Date });
        builder.HasIndex(s => s.SubmittedAt);

        builder.HasOne<School>().WithMany().HasForeignKey(s => s.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Issues).WithOne().HasForeignKey(i => i.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Issues).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AttendanceSubmissionIssueConfiguration : IEntityTypeConfiguration<AttendanceSubmissionIssue>
{
    public void Configure(EntityTypeBuilder<AttendanceSubmissionIssue> builder)
    {
        builder.ToTable("AttendanceSubmissionIssue", t =>
            t.HasCheckConstraint("CK_AttendanceSubmissionIssue_Reason", EnumColumns.CheckSql<SubmissionIssueReason>("Reason")));
        builder.Property(i => i.Code).HasMaxLength(AttendanceSubmissionIssue.MaxCodeLength);
        builder.Property(i => i.Reason).StoredAsName(40);
        builder.Property(i => i.Detail).HasMaxLength(AttendanceSubmissionIssue.MaxDetailLength);
        builder.HasOne<Student>().WithMany().HasForeignKey(i => i.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
