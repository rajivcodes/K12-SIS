namespace AttendanceApi.Domain.Common;

/// <summary>Argument checks shared by the entities. Each failure throws <see cref="DomainException"/>.</summary>
internal static class Guard
{
    public static string Required(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, maxLength, name);
    }

    public static DateTime Utc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException($"{name} must be a UTC timestamp.");
        }

        return value;
    }

    public static int Positive(int value, string name)
    {
        if (value <= 0)
        {
            throw new DomainException($"{name} must be greater than zero.");
        }

        return value;
    }

    public static int? NonNegative(int? value, string name)
    {
        if (value < 0)
        {
            throw new DomainException($"{name} cannot be negative.");
        }

        return value;
    }
}
