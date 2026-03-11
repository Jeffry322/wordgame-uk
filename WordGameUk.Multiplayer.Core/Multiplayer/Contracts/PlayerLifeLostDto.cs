namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record PlayerLifeLostDto(
    string PlayerId,
    int Lives,
    bool IsEliminated);
