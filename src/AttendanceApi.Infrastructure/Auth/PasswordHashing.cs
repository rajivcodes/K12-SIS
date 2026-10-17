using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace AttendanceApi.Infrastructure.Auth;

internal sealed class PasswordHashing : IPasswordHashing
{
    private readonly IPasswordHasher<AppUser> _hasher;

    public PasswordHashing(IPasswordHasher<AppUser> hasher)
    {
        _hasher = hasher;
    }

    public string Hash(AppUser user, string password) => _hasher.HashPassword(user, password);

    public PasswordCheck Verify(AppUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}
