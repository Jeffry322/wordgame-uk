namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record RoomPlayerDto(
    string PlayerId,
    string Name,
    int Lives,
    bool IsEliminated,
    bool IsConnected,
    bool IsInGame,
    bool IsCurrentTurn);
