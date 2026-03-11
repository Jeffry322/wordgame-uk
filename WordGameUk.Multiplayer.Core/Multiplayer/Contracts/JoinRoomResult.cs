namespace WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

public enum JoinRoomResult
{
    Joined = 0,
    Reconnected = 1,
    AlreadyInRoom = 2,
    NotFound = 3,
    Unavailable = 4
}
