namespace WordGameUk.Multiplayer.Contracts;

public sealed record RoomSnapshotDto(
    string RoomId,
    string Name,
    SyllableDifficulty Difficulty,
    int RoundSeconds,
    int MaxLives,
    bool IsInProgress,
    bool IsFinished,
    string CurrentFragment,
    string CurrentTurnConnectionId,
    int SecondsLeft,
    string WinnerConnectionId,
    string WinnerName,
    IReadOnlyCollection<RoomPlayerDto> Players,
    string StatusMessage);
