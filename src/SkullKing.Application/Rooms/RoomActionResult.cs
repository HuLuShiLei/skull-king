namespace SkullKing.Application.Rooms;

public readonly record struct RoomActionResult(bool Ok, string? Error)
{
    public static RoomActionResult Success { get; } = new(true, null);

    public static RoomActionResult Fail(string error) => new(false, error);
}
