using AttendanceApi.Domain.Users;

namespace AttendanceApi.Application.Abstractions;

public enum PasswordCheck
{
    Failed = 1,
    Success = 2,

    /// <summary>Correct, but the hash uses old settings and should be replaced.</summary>
    SuccessRehashNeeded = 3,
}

/// <summary>Password hashing (ASP.NET Core PasswordHasher in Infrastructure).</summary>
public interface IPasswordHashing
{
    string Hash(AppUser user, string password);

    PasswordCheck Verify(AppUser user, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Signs the API's own JWTs (plan decision 11).</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AppUser user);
}
