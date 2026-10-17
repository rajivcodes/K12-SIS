using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Attendance;

/// <summary>A district-wide attendance code. Codes are stored upper-case and matched case-insensitively.</summary>
public sealed class AttendanceCode
{
    public const int MaxCodeLength = 10;
    public const int MaxDescriptionLength = 100;

    private AttendanceCode()
    {
        Code = string.Empty;
        Description = string.Empty;
    }

    private AttendanceCode(string code, string description, bool isAbsent, bool isExcused, bool isActive)
    {
        Code = Normalize(code);
        Description = Guard.Required(description, MaxDescriptionLength, "Description");
        IsAbsent = isAbsent;
        IsExcused = isExcused;
        IsActive = isActive;
    }

    public int Id { get; private set; }

    public string Code { get; private set; }

    public string Description { get; private set; }

    public bool IsAbsent { get; private set; }

    public bool IsExcused { get; private set; }

    public bool IsActive { get; private set; }

    public static AttendanceCode Create(string code, string description, bool isAbsent, bool isExcused, bool isActive = true) =>
        new(code, description, isAbsent, isExcused, isActive);

    /// <summary>Trims and upper-cases a code for storage and comparison.</summary>
    public static string Normalize(string code) =>
        Guard.Required(code, MaxCodeLength, "Code").ToUpperInvariant();

    public void Update(string description, bool isAbsent, bool isExcused, bool isActive)
    {
        Description = Guard.Required(description, MaxDescriptionLength, "Description");
        IsAbsent = isAbsent;
        IsExcused = isExcused;
        IsActive = isActive;
    }
}
