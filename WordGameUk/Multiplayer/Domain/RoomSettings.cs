namespace WordGameUk.Multiplayer.Domain;

public sealed record RoomSettings(
    SyllableDifficulty Difficulty,
    int RoundSeconds,
    int MaxLives);
