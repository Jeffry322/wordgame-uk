using WordGameUk.Multiplayer.Core.Multiplayer.Domain;

namespace WordGameUk.Multiplayer.Core.Multiplayer.Services;

public sealed record RoomChange(
    string RoomId,
    bool RoomDeleted,
    MultiplayerGameRoom? Room);
