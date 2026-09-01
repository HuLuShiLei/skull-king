using System.Security.Cryptography;

namespace SkullKing.Application.Rooms;

public static class RoomCode
{
    /// <summary>去掉了 0/O/1/I/L 这类口头转述时容易听错抄错的字符。</summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];

        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? code)
    {
        var normalized = Normalize(code);

        return normalized.Length == Length && normalized.All(Alphabet.Contains);
    }
}
