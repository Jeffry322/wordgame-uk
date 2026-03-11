namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record LobbyRoomDto(
    string RoomId,
    string Name,
    int PlayersCount,
    bool IsInProgress,
    SyllableDifficulty Difficulty,
    int MaxLives);
