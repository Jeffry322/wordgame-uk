using Microsoft.AspNetCore.SignalR;
using WordGameUk.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Domain;
using WordGameUk.Multiplayer.Services;

namespace WordGameUk.Multiplayer;

public sealed class MultiplayerHub : Hub
{
    private readonly IMultiplayerLobbyService _lobbyService;

    public MultiplayerHub(IMultiplayerLobbyService lobbyService)
    {
        _lobbyService = lobbyService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await Clients.Caller.SendAsync("LobbyUpdated", _lobbyService.GetLobbySnapshot());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var room in _lobbyService.GetRooms())
        {
            if (!_lobbyService.TryLeaveRoom(room.Id, Context.ConnectionId, out var roomDeleted, out var updatedRoom))
                continue;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(room.Id));
            await BroadcastRoomChange(room.Id, roomDeleted, updatedRoom);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SetDisplayName(string displayName)
    {
        _lobbyService.SetDisplayName(Context.ConnectionId, displayName.Trim());
        await Clients.Caller.SendAsync("DisplayNameUpdated", _lobbyService.GetDisplayName(Context.ConnectionId));
    }

    public async Task<string?> CreateRoom(string roomName, SyllableDifficulty difficulty, int roundSeconds, int lives)
    {
        var normalizedName = string.IsNullOrWhiteSpace(roomName) ? "New Room" : roomName.Trim();
        var settings = new RoomSettings(
            difficulty,
            Math.Clamp(roundSeconds, 5, 30),
            Math.Clamp(lives, 1, 10));

        if (!_lobbyService.TryCreateRoom(normalizedName, settings, Context.ConnectionId, out var room))
            return null;
        await BroadcastRoomChange(room.Id, false, room);
        return room.Id;
    }

    public async Task<bool> JoinRoom(string roomId)
    {
        if (!_lobbyService.TryJoinRoom(roomId, Context.ConnectionId, out var room))
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
        await BroadcastRoomChange(roomId, false, room);
        return true;
    }

    public async Task LeaveRoom(string roomId)
    {
        if (!_lobbyService.TryLeaveRoom(roomId, Context.ConnectionId, out var roomDeleted, out var room))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));
        await BroadcastRoomChange(roomId, roomDeleted, room);
    }

    public async Task StartGame(string roomId)
    {
        if (!_lobbyService.TryStartRoom(roomId, out var room))
            return;

        await BroadcastRoomChange(roomId, false, room);
    }

    public async Task<WordGuessOutcome> SubmitWord(string roomId, string word)
    {
        var outcome = _lobbyService.TrySubmitWord(roomId, Context.ConnectionId, word.Trim(), out var room);
        if (outcome == WordGuessOutcome.Rejected || room is null)
            return WordGuessOutcome.Rejected;

        await Clients.Group(GroupName(roomId))
            .SendAsync("GuessFeedback", new GuessFeedbackDto(Context.ConnectionId, outcome));

        await BroadcastRoomChange(roomId, false, room);
        return outcome;
    }

    private async Task BroadcastRoomChange(string roomId, bool roomDeleted, MultiplayerGameRoom? room)
    {
        await Clients.All.SendAsync("LobbyUpdated", _lobbyService.GetLobbySnapshot());

        if (roomDeleted)
        {
            await Clients.Group(GroupName(roomId)).SendAsync("RoomClosed", roomId);
            return;
        }

        if (room is null)
            return;

        var snapshot = room.ToSnapshot(DateTimeOffset.UtcNow);
        await Clients.Group(GroupName(roomId)).SendAsync("RoomUpdated", snapshot);
    }

    private static string GroupName(string roomId) => $"room:{roomId}";
}
