namespace SkullKing.Domain.Game;

/// <summary>非法操作。正常客户端不会触发，服务端据此拒绝请求。</summary>
public sealed class GameRuleException(string message) : Exception(message);
