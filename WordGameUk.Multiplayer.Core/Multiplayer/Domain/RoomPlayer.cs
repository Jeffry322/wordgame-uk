namespace WordGameUk.Multiplayer.Core.Multiplayer.Domain;

public sealed class RoomPlayer
{
    public RoomPlayer(string playerId, string connectionId, string name, int startingLives, int order)
    {
        PlayerId = playerId;
        ConnectionId = connectionId;
        Name = name;
        Lives = startingLives;
        Order = order;
    }

    public string PlayerId { get; }
    public string ConnectionId { get; private set; }
    public string Name { get; private set; }
    public int Lives { get; private set; }
    public int Order { get; }
    public bool IsInGame { get; private set; }
    public DateTimeOffset? DisconnectedAtUtc { get; private set; }
    public bool IsEliminated => Lives <= 0;
    public bool IsConnected => !string.IsNullOrWhiteSpace(ConnectionId);

    public void Rename(string name) => Name = name;

    public void Connect(string connectionId)
    {
        ConnectionId = connectionId;
        DisconnectedAtUtc = null;
    }

    public void Disconnect(DateTimeOffset nowUtc)
    {
        ConnectionId = string.Empty;
        DisconnectedAtUtc ??= nowUtc;
    }

    public void LoseLife()
    {
        if (Lives > 0)
            Lives--;
    }

    public void JoinGame(int startingLives)
    {
        IsInGame = true;
        Lives = startingLives;
    }

    public void LeaveGame(int startingLives)
    {
        IsInGame = false;
        Lives = startingLives;
    }
}
