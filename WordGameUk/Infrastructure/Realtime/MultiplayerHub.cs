using Microsoft.AspNetCore.SignalR;
using WordGameUk.Multiplayer.Core;
using WordGameUk.Multiplayer.Core.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Core.Multiplayer.Domain;
using WordGameUk.Multiplayer.Core.Multiplayer.Services;

namespace WordGameUk.Infrastructure.Realtime;

public sealed class MultiplayerHub(
    IMultiplayerLobbyService lobbyService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("LobbyUpdated", lobbyService.GetLobbySnapshot());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var room in lobbyService.GetRooms())
        {
            if (!lobbyService.TryLeaveRoom(room.Id, Context.ConnectionId, out var roomDeleted, out var updatedRoom))
                continue;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(room.Id));
            await BroadcastRoomChange(room.Id, roomDeleted, updatedRoom);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SetDisplayName(string playerId, string displayName)
    {
        var normalizedPlayerId = playerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            return;

        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
        lobbyService.SetDisplayName(normalizedPlayerId, normalizedDisplayName);
        await Clients.Caller.SendAsync("DisplayNameUpdated", lobbyService.GetDisplayName(normalizedPlayerId));
    }

    public async Task<string?> CreateRoom(string roomName, SyllableDifficulty difficulty, int roundSeconds, int lives)
    {
        var normalizedName = string.IsNullOrWhiteSpace(roomName) ? "New Room" : roomName.Trim();
        var settings = new RoomSettings(
            difficulty,
            Math.Clamp(roundSeconds, 5, 30),
            Math.Clamp(lives, 1, 10));

        if (!lobbyService.TryCreateRoom(normalizedName, settings, out var room))
            return null;
        await BroadcastRoomChange(room.Id, false, room);
        return room.Id;
    }

    public async Task<JoinRoomResult> JoinRoom(string roomId, string playerId)
    {
        var normalizedPlayerId = playerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            return JoinRoomResult.Unavailable;

        var result = lobbyService.TryJoinRoom(roomId, normalizedPlayerId, Context.ConnectionId, out var room);
        if (result is not (JoinRoomResult.Joined or JoinRoomResult.Reconnected) || room is null)
            return result;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
        await BroadcastRoomChange(roomId, false, room);
        return result;
    }

    public async Task SetPlayerInGame(string roomId, string playerId, bool isInGame)
    {
        var normalizedPlayerId = playerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            return;

        if (!lobbyService.TrySetPlayerInGame(roomId, normalizedPlayerId, Context.ConnectionId, isInGame, out var room) || room is null)
            return;

        await BroadcastRoomChange(roomId, false, room);
    }

    public async Task LeaveRoom(string roomId)
    {
        if (!lobbyService.TryLeaveRoom(roomId, Context.ConnectionId, out var roomDeleted, out var room))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));
        await BroadcastRoomChange(roomId, roomDeleted, room);
    }

    public async Task StartGame(string roomId)
    {
        if (!lobbyService.TryStartRoom(roomId, out var room))
            return;

        await BroadcastRoomChange(roomId, false, room);
    }

    public async Task UpdateTyping(string roomId, string playerId, string text)
    {
        var normalizedPlayerId = playerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            return;

        if (!lobbyService.CanPlayerGuess(roomId, normalizedPlayerId, Context.ConnectionId))
            return;

        var normalizedText = text.Trim();
        if (normalizedText.Length > 32)
            normalizedText = normalizedText[..32];

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("TypingUpdated", new TypingUpdateDto(normalizedPlayerId, normalizedText));
    }

    public async Task<WordGuessOutcome> SubmitWord(string roomId, string playerId, string word)
    {
        var normalizedPlayerId = playerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            return WordGuessOutcome.Rejected;

        var outcome = lobbyService.TrySubmitWord(roomId, normalizedPlayerId, Context.ConnectionId, word.Trim(), out var room);
        if (outcome == WordGuessOutcome.Rejected || room is null)
            return WordGuessOutcome.Rejected;

        await Clients.Group(GroupName(roomId))
            .SendAsync("TypingUpdated", new TypingUpdateDto(normalizedPlayerId, string.Empty));

        await Clients.Group(GroupName(roomId))
            .SendAsync("GuessFeedback", new GuessFeedbackDto(normalizedPlayerId, outcome));

        await BroadcastRoomChange(roomId, false, room);
        return outcome;
    }

    private async Task BroadcastRoomChange(string roomId, bool roomDeleted, MultiplayerGameRoom? room)
    {
        await Clients.All.SendAsync("LobbyUpdated", lobbyService.GetLobbySnapshot());

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
