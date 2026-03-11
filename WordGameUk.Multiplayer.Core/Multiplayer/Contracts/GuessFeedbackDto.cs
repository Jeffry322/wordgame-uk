namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public sealed record GuessFeedbackDto(
    string PlayerId,
    WordGuessOutcome Outcome);
