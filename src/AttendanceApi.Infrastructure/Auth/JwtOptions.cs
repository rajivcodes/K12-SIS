using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AttendanceApi.Infrastructure.Auth;

/// <summary>Bound from the "Jwt" section. The signing key is a secret (user-secrets or environment only).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = "AttendanceApi";

    [Required]
    public string Audience { get; set; } = "AttendanceApi";

    [Range(1, 24 * 60)]
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Base64 (preferred) or plain text; at least 32 bytes either way.</summary>
    public string? SigningKey { get; set; }

    public byte[] SigningKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            return [];
        }

        var buffer = new byte[SigningKey.Length];
        return Convert.TryFromBase64String(SigningKey, buffer, out var written)
            ? buffer[..written]
            : Encoding.UTF8.GetBytes(SigningKey);
    }
}
