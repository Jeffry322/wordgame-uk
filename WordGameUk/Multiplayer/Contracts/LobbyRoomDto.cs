namespace WordGameUk.Multiplayer.Contracts;

public sealed record LobbyRoomDto(
    string RoomId,
    string Name,
    int PlayersCount,
    bool IsInProgress,
    bool IsFinished,
    SyllableDifficulty Difficulty,
    int MaxLives);
