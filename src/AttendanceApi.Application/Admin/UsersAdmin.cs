using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Admin;

public sealed record UserResponse(
    int Id,
    string UserName,
    string DisplayName,
    string? Email,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<int> SchoolIds);

public sealed record CreateUserRequest(
    string? UserName,
    string? DisplayName,
    string? Email,
    string? Password,
    UserRole? Role,
    IReadOnlyList<int>? SchoolIds);

/// <summary>Full replacement of a user's profile, role, status and schools. <c>NewPassword</c> resets the password.</summary>
public sealed record UpdateUserRequest(
    string? DisplayName,
    string? Email,
    UserRole? Role,
    bool? IsActive,
    IReadOnlyList<int>? SchoolIds,
    string? NewPassword);

/// <summary>User accounts (plan §5): create, reset passwords, deactivate, assign roles and schools.</summary>
public sealed class UsersAdmin
{
    public const int MinPasswordLength = 12;

    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly IPasswordHashing _passwords;
    private readonly AuditLog _audit;

    public UsersAdmin(IAttendanceDbContext db, AccessGuard access, IPasswordHashing passwords, AuditLog audit)
    {
        _db = db;
        _access = access;
        _passwords = passwords;
        _audit = audit;
    }

    public async Task<List<UserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        var users = await _db.AppUsers.AsNoTracking().Include(u => u.Schools).OrderBy(u => u.UserName).ToListAsync(cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    public async Task<UserResponse> GetAsync(int userId, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        return ToResponse(await LoadAsync(userId, cancellationToken));
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        if (request.Role is not { } role || !Enum.IsDefined(role))
        {
            throw new RequestValidationException("role is required: Admin, AttendanceClerk or Viewer.");
        }

        ValidatePassword(request.Password);
        var userName = request.UserName?.Trim() ?? string.Empty;
        if (await _db.AppUsers.AnyAsync(u => u.UserName == userName, cancellationToken))
        {
            throw new ConflictException($"User name '{userName}' is already taken.");
        }

        var schoolIds = await ValidateSchoolsAsync(request.SchoolIds, cancellationToken);
        var user = AppUser.Create(userName, request.DisplayName ?? string.Empty, request.Email, "pending", role);
        user.SetPasswordHash(_passwords.Hash(user, request.Password!));
        user.AssignSchools(schoolIds);
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        _audit.Add("UserCreated", "AppUser", user.Id, new { user.UserName, role, schoolIds });
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        if (request.Role is not { } role || !Enum.IsDefined(role) || request.IsActive is not { } isActive)
        {
            throw new RequestValidationException("role and isActive are required.");
        }

        var user = await LoadAsync(userId, cancellationToken);
        var stopsBeingActiveAdmin = user.Role == UserRole.Admin && user.IsActive && (role != UserRole.Admin || !isActive);
        if (stopsBeingActiveAdmin
            && !await _db.AppUsers.AnyAsync(u => u.Id != userId && u.Role == UserRole.Admin && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Last admin", "This is the only active admin; demoting or deactivating it would lock everyone out of administration.", "LastActiveAdmin");
        }

        var schoolIds = await ValidateSchoolsAsync(request.SchoolIds, cancellationToken);
        user.UpdateProfile(request.DisplayName ?? string.Empty, request.Email);
        user.ChangeRole(role);
        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        user.AssignSchools(schoolIds);
        if (request.NewPassword is not null)
        {
            ValidatePassword(request.NewPassword);
            user.SetPasswordHash(_passwords.Hash(user, request.NewPassword));
        }

        _audit.Add("UserUpdated", "AppUser", user.Id, new { role, isActive, schoolIds, passwordReset = request.NewPassword is not null });
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task<AppUser> LoadAsync(int userId, CancellationToken cancellationToken) =>
        await _db.AppUsers.Include(u => u.Schools).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException($"User {userId} does not exist.");

    private async Task<List<int>> ValidateSchoolsAsync(IReadOnlyList<int>? schoolIds, CancellationToken cancellationToken)
    {
        var ids = (schoolIds ?? []).Distinct().ToList();
        var existing = await _db.Schools.Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken);
        var unknown = ids.Except(existing).ToList();
        return unknown.Count == 0
            ? ids
            : throw new RequestValidationException($"Unknown school ID(s): {string.Join(", ", unknown)}.");
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw new RequestValidationException($"Password must be at least {MinPasswordLength} characters.");
        }
    }

    private static UserResponse ToResponse(AppUser u) => new(
        u.Id, u.UserName, u.DisplayName, u.Email, u.Role, u.IsActive, u.LastLoginAt,
        u.Schools.Select(s => s.SchoolId).Order().ToList());
}
