namespace SkullKing.Contracts;

public sealed record AnonymousLoginRequest(string Nickname);

/// <summary>
/// Token 同时是身份凭证和座位凭证：断线后凭它重连回原座位。
/// </summary>
public sealed record AuthResponse(string PlayerId, string Nickname, string Token);

public sealed record RenameRequest(string Nickname);
