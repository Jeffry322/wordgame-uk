namespace WordGameUk.Infrastructure.Http;

public sealed record PresenceDisconnectRequest(
    string RoomId,
    string PlayerId);
