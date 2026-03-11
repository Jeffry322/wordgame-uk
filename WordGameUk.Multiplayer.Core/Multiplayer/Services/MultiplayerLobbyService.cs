using System.Collections.Concurrent;
using WordGameUk.Multiplayer.Core.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Core.Multiplayer.Domain;

namespace WordGameUk.Multiplayer.Core.Multiplayer.Services;

public sealed class MultiplayerLobbyService : IMultiplayerLobbyService
{
    private static readonly TimeSpan WaitingDisconnectGrace = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<string, MultiplayerGameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _displayNames = new();
    private readonly IWordDictionary _dictionary;

    public MultiplayerLobbyService(IWordDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public void SetDisplayName(string playerId, string displayName)
    {
        _displayNames[playerId] = displayName;

        foreach (var room in _rooms.Values)
            room.RenamePlayer(playerId, displayName);
    }

    public string GetDisplayName(string playerId)
    {
        return _displayNames.TryGetValue(playerId, out var displayName)
            ? displayName
            : $"Player-{playerId[..Math.Min(6, playerId.Length)]}";
    }

    public bool TryCreateRoom(string roomName, RoomSettings settings, out MultiplayerGameRoom room)
    {
        var id = Guid.NewGuid().ToString("N");
        room = new MultiplayerGameRoom(id, roomName, settings, _dictionary);

        if (!_rooms.TryAdd(id, room))
            return false;
        return true;
    }

    public JoinRoomResult TryJoinRoom(string roomId, string playerId, string connectionId, out MultiplayerGameRoom? room)
    {
        room = null;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return JoinRoomResult.NotFound;

        var joinResult = existingRoom.TryJoinPlayer(playerId, connectionId, GetDisplayName(playerId));
        if (joinResult is not (JoinRoomResult.Joined or JoinRoomResult.Reconnected))
            return joinResult;

        room = existingRoom;
        return joinResult;
    }

    public bool TryLeaveRoom(string roomId, string connectionId, out bool roomDeleted, out MultiplayerGameRoom? room)
    {
        roomDeleted = false;
        room = null;

        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.RemoveOrDisconnectPlayerByConnection(connectionId))
            return false;

        if (existingRoom.IsEmpty())
        {
            roomDeleted = _rooms.TryRemove(roomId, out _);
            if (!roomDeleted)
                room = existingRoom;
            return true;
        }

        room = existingRoom;
        return true;
    }

    public bool TryLeaveRoomByPlayerId(string roomId, string playerId, out bool roomDeleted, out MultiplayerGameRoom? room)
    {
        roomDeleted = false;
        room = null;

        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.RemoveOrDisconnectPlayerByPlayerId(playerId))
            return false;

        if (existingRoom.IsEmpty())
        {
            roomDeleted = _rooms.TryRemove(roomId, out _);
            if (!roomDeleted)
                room = existingRoom;
            return true;
        }

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

    public bool TrySetPlayerInGame(string roomId, string playerId, string connectionId, bool isInGame, out MultiplayerGameRoom? room)
    {
        room = null;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        if (!existingRoom.TrySetPlayerInGame(playerId, connectionId, isInGame))
            return false;

        room = existingRoom;
        return true;
    }

    public bool CanPlayerGuess(string roomId, string playerId, string connectionId)
    {
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return false;

        return existingRoom.CanPlayerGuess(playerId, connectionId);
    }

    public WordGuessOutcome TrySubmitWord(string roomId, string playerId, string connectionId, string word, out MultiplayerGameRoom? room)
    {
        room = null;
        if (!_rooms.TryGetValue(roomId, out var existingRoom))
            return WordGuessOutcome.Rejected;

        var outcome = existingRoom.TrySubmitWord(playerId, connectionId, word);
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

    public IReadOnlyCollection<RoomChange> PruneDisconnectedWaitingPlayers(DateTimeOffset nowUtc)
    {
        var changes = new List<RoomChange>();

        foreach (var (roomId, room) in _rooms.ToArray())
        {
            if (!room.PruneDisconnectedPlayers(nowUtc, WaitingDisconnectGrace))
                continue;

            if (room.IsEmpty())
            {
                var roomDeleted = _rooms.TryRemove(roomId, out _);
                changes.Add(new RoomChange(roomId, roomDeleted, roomDeleted ? null : room));
                continue;
            }

            changes.Add(new RoomChange(roomId, false, room));
        }

        return changes;
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
