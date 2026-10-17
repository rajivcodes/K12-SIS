using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class SchoolYearConfiguration : IEntityTypeConfiguration<SchoolYear>
{
    public void Configure(EntityTypeBuilder<SchoolYear> builder)
    {
        // Years of one school must not overlap; that cross-row rule is enforced by SchoolYear.EnsureNoConflictWith.
        builder.ToTable("SchoolYear", t =>
        {
            t.HasCheckConstraint("CK_SchoolYear_Dates", "[EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_SchoolYear_Label", "[Label] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
        });
        builder.Property(y => y.Label).HasMaxLength(9).IsUnicode(false);
        builder.HasIndex(y => new { y.SchoolId, y.Label }).IsUnique();
        builder.HasIndex(y => new { y.SchoolId, y.StartDate, y.EndDate });

        builder.HasOne<School>().WithMany().HasForeignKey(y => y.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(y => y.Terms).WithOne().HasForeignKey(t => t.SchoolYearId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(y => y.Terms).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        // Terms must sit inside their year and not overlap; enforced by SchoolYear.AddTerm/ChangeTerm.
        builder.ToTable("Term", t => t.HasCheckConstraint("CK_Term_Dates", "[EndDate] >= [StartDate]"));
        builder.Property(t => t.Name).HasMaxLength(Term.MaxNameLength);
        builder.HasIndex(t => new { t.SchoolYearId, t.StartDate });
    }
}

internal sealed class NonInstructionalDayConfiguration : IEntityTypeConfiguration<NonInstructionalDay>
{
    public void Configure(EntityTypeBuilder<NonInstructionalDay> builder)
    {
        builder.ToTable("NonInstructionalDay");
        builder.Property(d => d.Reason).HasMaxLength(NonInstructionalDay.MaxReasonLength);
        builder.HasIndex(d => new { d.SchoolId, d.Date }).IsUnique();
        builder.HasOne<School>().WithMany().HasForeignKey(d => d.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
