using WordGameUk.Multiplayer.Core.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Core.Multiplayer.Domain;

namespace WordGameUk.Multiplayer.Core.Multiplayer.Services;

public interface IMultiplayerLobbyService
{
    void SetDisplayName(string playerId, string displayName);
    string GetDisplayName(string playerId);
    bool TryCreateRoom(string roomName, RoomSettings settings, out MultiplayerGameRoom room);
    JoinRoomResult TryJoinRoom(string roomId, string playerId, string connectionId, out MultiplayerGameRoom? room);
    bool TryLeaveRoom(string roomId, string connectionId, out bool roomDeleted, out MultiplayerGameRoom? room);
    bool TryLeaveRoomByPlayerId(string roomId, string playerId, out bool roomDeleted, out MultiplayerGameRoom? room);
    bool TrySetPlayerInGame(string roomId, string playerId, string connectionId, bool isInGame, out MultiplayerGameRoom? room);
    bool TryStartRoom(string roomId, out MultiplayerGameRoom room);
    bool CanPlayerGuess(string roomId, string playerId, string connectionId);
    WordGuessOutcome TrySubmitWord(string roomId, string playerId, string connectionId, string word, out MultiplayerGameRoom? room);
    bool TryApplyTimeout(string roomId, DateTimeOffset nowUtc, out MultiplayerGameRoom room, out PlayerLifeLostDto? lifeLost);
    IReadOnlyCollection<RoomChange> PruneDisconnectedWaitingPlayers(DateTimeOffset nowUtc);
    IReadOnlyCollection<MultiplayerGameRoom> GetRooms();
    IReadOnlyCollection<LobbyRoomDto> GetLobbySnapshot();
}
