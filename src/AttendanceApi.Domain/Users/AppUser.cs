using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Users;

/// <summary>An API-issued user account. The password hash comes from ASP.NET Core's PasswordHasher.</summary>
public sealed class AppUser
{
    public const int MaxUserNameLength = 100;
    public const int MaxDisplayNameLength = 200;
    public const int MaxEmailLength = 256;

    private readonly List<AppUserSchool> _schools = [];

    private AppUser()
    {
        UserName = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
    }

    private AppUser(string userName, string displayName, string? email, string passwordHash, UserRole role)
    {
        UserName = Guard.Required(userName, MaxUserNameLength, "User name");
        DisplayName = Guard.Required(displayName, MaxDisplayNameLength, "Display name");
        Email = Guard.Optional(email, MaxEmailLength, "Email");
        PasswordHash = RequireHash(passwordHash);
        Role = RequireRole(role);
        IsActive = true;
    }

    public int Id { get; private set; }

    public string UserName { get; private set; }

    public string DisplayName { get; private set; }

    public string? Email { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public IReadOnlyCollection<AppUserSchool> Schools => _schools.AsReadOnly();

    public static AppUser Create(string userName, string displayName, string? email, string passwordHash, UserRole role) =>
        new(userName, displayName, email, passwordHash, role);

    public void UpdateProfile(string displayName, string? email)
    {
        DisplayName = Guard.Required(displayName, MaxDisplayNameLength, "Display name");
        Email = Guard.Optional(email, MaxEmailLength, "Email");
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = RequireHash(passwordHash);

    public void ChangeRole(UserRole role) => Role = RequireRole(role);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void RecordLogin(DateTime utcNow) => LastLoginAt = Guard.Utc(utcNow, nameof(utcNow));

    /// <summary>Replaces the schools this user may access.</summary>
    public void AssignSchools(IEnumerable<int> schoolIds)
    {
        var wanted = schoolIds.ToHashSet();
        _schools.RemoveAll(s => !wanted.Contains(s.SchoolId));
        foreach (var schoolId in wanted.Where(id => _schools.TrueForAll(s => s.SchoolId != id)))
        {
            _schools.Add(new AppUserSchool(Id, schoolId));
        }
    }

    public bool CanAccessSchool(int schoolId) =>
        Role.CanAccessSchool(schoolId, _schools.Select(s => s.SchoolId));

    private static string RequireHash(string passwordHash) =>
        string.IsNullOrWhiteSpace(passwordHash)
            ? throw new DomainException("Password hash is required.")
            : passwordHash;

    private static UserRole RequireRole(UserRole role) =>
        Enum.IsDefined(role) ? role : throw new DomainException($"Unknown role '{role}'.");
}
