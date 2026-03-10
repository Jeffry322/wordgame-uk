namespace WordGameUk.Multiplayer.Contracts;

public sealed record RoomPlayerDto(
    string ConnectionId,
    string Name,
    int Lives,
    bool IsEliminated,
    bool IsCurrentTurn);
