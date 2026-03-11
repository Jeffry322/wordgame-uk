using Microsoft.AspNetCore.SignalR;
using WordGameUk.Multiplayer.Core.Multiplayer.Services;

namespace WordGameUk.Infrastructure.Realtime;

public sealed class MultiplayerRoomTickerService : BackgroundService
{
    private readonly IMultiplayerLobbyService _lobbyService;
    private readonly IHubContext<MultiplayerHub> _hubContext;

    public MultiplayerRoomTickerService(
        IMultiplayerLobbyService lobbyService,
        IHubContext<MultiplayerHub> hubContext)
    {
        _lobbyService = lobbyService;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var roomChanged = false;

            foreach (var room in _lobbyService.GetRooms())
            {
                if (!_lobbyService.TryApplyTimeout(room.Id, now, out var updatedRoom, out var lifeLost))
                    continue;

                roomChanged = true;
                if (lifeLost is not null)
                {
                    await _hubContext.Clients.Group(GroupName(room.Id))
                        .SendAsync("PlayerLifeLost", lifeLost, stoppingToken);
                }

                var snapshot = updatedRoom.ToSnapshot(now);
                await _hubContext.Clients.Group(GroupName(room.Id)).SendAsync("RoomUpdated", snapshot, stoppingToken);
            }

            var pruneChanges = _lobbyService.PruneDisconnectedWaitingPlayers(now);
            if (pruneChanges.Count > 0)
            {
                roomChanged = true;
                foreach (var change in pruneChanges)
                {
                    var groupName = GroupName(change.RoomId);
                    if (change.RoomDeleted)
                    {
                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("RoomClosed", change.RoomId, stoppingToken);
                        continue;
                    }

                    if (change.Room is null)
                        continue;

                    var snapshot = change.Room.ToSnapshot(now);
                    await _hubContext.Clients.Group(groupName).SendAsync("RoomUpdated", snapshot, stoppingToken);
                }
            }

            if (roomChanged)
                await _hubContext.Clients.All.SendAsync("LobbyUpdated", _lobbyService.GetLobbySnapshot(), stoppingToken);

            foreach (var room in _lobbyService.GetRooms())
            {
                var snapshot = room.ToSnapshot(DateTimeOffset.UtcNow);
                if (!snapshot.IsInProgress)
                    continue;

                await _hubContext.Clients.Group(GroupName(room.Id)).SendAsync("RoomUpdated", snapshot, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private static string GroupName(string roomId) => $"room:{roomId}";
}
