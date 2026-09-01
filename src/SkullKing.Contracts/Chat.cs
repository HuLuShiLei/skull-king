namespace SkullKing.Contracts;

public sealed record ChatMessageDto(
    string Id,
    string PlayerId,
    string Nickname,
    int Seat,
    string Text,
    DateTimeOffset SentAt);

public sealed record SendChatRequest(string RoomCode, string Text);
