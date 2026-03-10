namespace WordGameUk.Multiplayer.Domain;

public sealed class RoomPlayer
{
    public RoomPlayer(string connectionId, string name, int startingLives)
    {
        ConnectionId = connectionId;
        Name = name;
        Lives = startingLives;
    }

    public string ConnectionId { get; }
    public string Name { get; private set; }
    public int Lives { get; private set; }
    public bool IsEliminated => Lives <= 0;

    public void Rename(string name) => Name = name;
    public void LoseLife()
    {
        if (Lives > 0)
            Lives--;
    }
}
