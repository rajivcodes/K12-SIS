using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUser", t =>
            t.HasCheckConstraint("CK_AppUser_Role", EnumColumns.CheckSql<UserRole>("Role")));
        builder.Property(u => u.UserName).HasMaxLength(AppUser.MaxUserNameLength);
        builder.Property(u => u.DisplayName).HasMaxLength(AppUser.MaxDisplayNameLength);
        builder.Property(u => u.Email).HasMaxLength(AppUser.MaxEmailLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsUnicode(false);
        builder.Property(u => u.Role).StoredAsName(20);
        builder.HasIndex(u => u.UserName).IsUnique();

        builder.HasMany(u => u.Schools).WithOne().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.Schools).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AppUserSchoolConfiguration : IEntityTypeConfiguration<AppUserSchool>
{
    public void Configure(EntityTypeBuilder<AppUserSchool> builder)
    {
        builder.ToTable("AppUserSchool");
        builder.HasKey(s => new { s.UserId, s.SchoolId });
        builder.HasOne<School>().WithMany().HasForeignKey(s => s.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
