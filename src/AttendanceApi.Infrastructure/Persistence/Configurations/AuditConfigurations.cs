using AttendanceApi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntry");
        builder.Property(a => a.UserName).HasMaxLength(Actor.MaxNameLength);
        builder.Property(a => a.Action).HasMaxLength(AuditEntry.MaxActionLength).IsUnicode(false);
        builder.Property(a => a.EntityType).HasMaxLength(AuditEntry.MaxEntityTypeLength).IsUnicode(false);
        builder.Property(a => a.EntityId).HasMaxLength(AuditEntry.MaxEntityIdLength);
        builder.Property(a => a.Details).HasMaxLength(AuditEntry.MaxDetailsLength);
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        // No FK to AppUser: system writes (import, demo seed) have no user, and the log must outlive accounts.
    }
}

internal sealed class LegacyIdMapConfiguration : IEntityTypeConfiguration<LegacyIdMap>
{
    public void Configure(EntityTypeBuilder<LegacyIdMap> builder)
    {
        builder.ToTable("LegacyIdMap");
        builder.HasKey(m => new { m.EntityType, m.LegacyId });
        builder.Property(m => m.EntityType).HasMaxLength(LegacyIdMap.MaxEntityTypeLength).IsUnicode(false);
        builder.Property(m => m.LegacyId).HasMaxLength(LegacyIdMap.MaxLegacyIdLength);
        builder.HasIndex(m => new { m.EntityType, m.NewId });
    }
}
