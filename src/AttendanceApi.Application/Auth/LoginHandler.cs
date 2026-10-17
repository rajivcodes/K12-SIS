using AttendanceApi.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Auth;

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record LoginUser(int Id, string UserName, string DisplayName, string Role);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, LoginUser User);

/// <summary>Exchanges a user name and password for a JWT. Returns null for any failure (no detail leaked).</summary>
public sealed class LoginHandler
{
    private readonly IAttendanceDbContext _db;
    private readonly IPasswordHashing _passwords;
    private readonly IAccessTokenIssuer _tokens;
    private readonly IClock _clock;

    public LoginHandler(IAttendanceDbContext db, IPasswordHashing passwords, IAccessTokenIssuer tokens, IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<LoginResponse?> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var userName = request.UserName.Trim();
        var user = await _db.AppUsers.SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var check = _passwords.Verify(user, request.Password);
        if (check == PasswordCheck.Failed)
        {
            return null;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.SetPasswordHash(_passwords.Hash(user, request.Password));
        }

        user.RecordLogin(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _tokens.Issue(user);
        return new LoginResponse(
            token.Token,
            "Bearer",
            token.ExpiresAt,
            new LoginUser(user.Id, user.UserName, user.DisplayName, user.Role.ToString()));
    }
}
