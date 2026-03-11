namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record TypingUpdateDto(
    string PlayerId,
    string Text);
