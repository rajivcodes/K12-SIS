using AttendanceApi.Domain.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("School");
        builder.Property(s => s.Name).HasMaxLength(School.MaxNameLength);
    }
}

internal sealed class SchoolAttendanceSettingsConfiguration : IEntityTypeConfiguration<SchoolAttendanceSettings>
{
    public void Configure(EntityTypeBuilder<SchoolAttendanceSettings> builder)
    {
        builder.ToTable("SchoolAttendanceSettings", t =>
        {
            t.HasCheckConstraint("CK_SchoolAttendanceSettings_ChronicMethod", EnumColumns.CheckSql<ChronicMethod>("ChronicMethod"));
            t.HasCheckConstraint("CK_SchoolAttendanceSettings_ChronicCountThreshold", "[ChronicCountThreshold] >= 1");
            t.HasCheckConstraint("CK_SchoolAttendanceSettings_ChronicRateThreshold", "[ChronicRateThreshold] > 0 AND [ChronicRateThreshold] <= 100");
            t.HasCheckConstraint("CK_SchoolAttendanceSettings_MaxBackdateDays", "[MaxBackdateDays] IS NULL OR [MaxBackdateDays] >= 0");
            t.HasCheckConstraint("CK_SchoolAttendanceSettings_MaxFutureDays", "[MaxFutureDays] IS NULL OR [MaxFutureDays] >= 0");
        });
        builder.HasKey(s => s.SchoolId);
        builder.Property(s => s.SchoolId).ValueGeneratedNever();
        builder.Property(s => s.ChronicMethod).StoredAsName(10);
        builder.Property(s => s.ChronicRateThreshold).HasPrecision(5, 2);

        builder.HasOne<School>().WithOne().HasForeignKey<SchoolAttendanceSettings>(s => s.SchoolId).OnDelete(DeleteBehavior.Cascade);
    }
}
