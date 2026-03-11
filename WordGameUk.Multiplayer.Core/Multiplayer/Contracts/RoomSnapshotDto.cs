namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record RoomSnapshotDto(
    string RoomId,
    string Name,
    SyllableDifficulty Difficulty,
    int RoundSeconds,
    int MaxLives,
    bool IsInProgress,
    string CurrentFragment,
    string CurrentTurnPlayerId,
    int SecondsLeft,
    IReadOnlyCollection<RoomPlayerDto> Players,
    string StatusMessage);
