using System.Security.Cryptography;
using System.Text;

namespace SkullKing.Application.Rooms;

/// <summary>
/// 房间密码只是「别让路人乱入」的门槛，不是账号凭证，所以带盐 SHA256 足够。
/// </summary>
public static class PasswordHasher
{
    public static string? Hash(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(password)]);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string? storedHash, string? password)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var parts = storedHash.Split('.');

        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(password)]);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
