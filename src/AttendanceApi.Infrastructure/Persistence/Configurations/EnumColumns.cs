using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceApi.Infrastructure.Persistence.Configurations;

/// <summary>Enums are stored by name, with a check constraint listing the allowed names.</summary>
internal static class EnumColumns
{
    public static PropertyBuilder<TEnum> StoredAsName<TEnum>(this PropertyBuilder<TEnum> property, int maxLength)
        where TEnum : struct, Enum =>
        property.HasConversion<string>().HasMaxLength(maxLength).IsUnicode(false);

    public static string CheckSql<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
