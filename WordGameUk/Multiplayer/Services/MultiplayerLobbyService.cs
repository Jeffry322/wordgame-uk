using System.Collections.Concurrent;
using WordGameUk.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Domain;

namespace WordGameUk.Multiplayer.Services;

public sealed class MultiplayerLobbyService : IMultiplayerLobbyService
{
    private readonly ConcurrentDictionary<string, MultiplayerGameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _displayNames = new();
    private readonly Dictionary _dictionary;

    public MultiplayerLobbyService(Dictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public void SetDisplayName(string connectionId, string displayName)
    {
        _displayNames[connectionId] = displayName;

        foreach (var room in _rooms.Values)
            room.RenamePlayer(connectionId, displayName);
    }

    public string GetDisplayName(string connectionId)
    {
        return _displayNames.TryGetValue(connectionId, out var displayName)
            ? displayName
            : $"Player-{connectionId[..Math.Min(6, connectionId.Length)]}";
    }

    public bool TryCreateRoom(string roomName, RoomSettings settings, string ownerConnectionId, out MultiplayerGameRoom room)
    {
        var id = Guid.NewGuid().ToString("N");
        room = new MultiplayerGameRoom(id, roomName, settings, _dictionary);

        if (!_rooms.TryAdd(id, room))
            return false;
        return true;
    }

    public bool TryJoinRoom(string roomId, string connectionId, out MultiplayerGameRoom room)
    {
        room = null!;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.TryAddPlayer(connectionId, GetDisplayName(connectionId)))
            return false;

        room = existingRoom;
        return true;
    }

    public bool TryLeaveRoom(string roomId, string connectionId, out bool roomDeleted, out MultiplayerGameRoom? room)
    {
        roomDeleted = false;
        room = null;

        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.RemovePlayer(connectionId))
            return false;

        room = existingRoom;
        return true;
    }

    public bool TryStartRoom(string roomId, out MultiplayerGameRoom room)
    {
        room = null!;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.TryStart())
            return false;

        room = existingRoom;
        return true;
    }

    public WordGuessOutcome TrySubmitWord(string roomId, string connectionId, string word, out MultiplayerGameRoom? room)
    {
        room = null;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return WordGuessOutcome.Rejected;

        var outcome = existingRoom.TrySubmitWord(connectionId, word);
        if (outcome == WordGuessOutcome.Rejected)
            return WordGuessOutcome.Rejected;

        room = existingRoom;
        return outcome;
    }

    public bool TryApplyTimeout(string roomId, DateTimeOffset nowUtc, out MultiplayerGameRoom room, out PlayerLifeLostDto? lifeLost)
    {
        room = null!;
        lifeLost = null;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.TryApplyTimeout(nowUtc, out lifeLost))
            return false;

        room = existingRoom;
        return true;
    }

    public IReadOnlyCollection<MultiplayerGameRoom> GetRooms()
    {
        return _rooms.Values.ToArray();
    }

    public IReadOnlyCollection<LobbyRoomDto> GetLobbySnapshot()
    {
        return _rooms.Values
            .Select(x => x.ToLobbyDto())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
