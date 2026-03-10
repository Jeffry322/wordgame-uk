using WordGameUk.Multiplayer.Contracts;
using WordGameUk.Multiplayer.Domain;

namespace WordGameUk.Multiplayer.Services;

public interface IMultiplayerLobbyService
{
    void SetDisplayName(string connectionId, string displayName);
    string GetDisplayName(string connectionId);
    bool TryCreateRoom(string roomName, RoomSettings settings, string ownerConnectionId, out MultiplayerGameRoom room);
    bool TryJoinRoom(string roomId, string connectionId, out MultiplayerGameRoom room);
    bool TryLeaveRoom(string roomId, string connectionId, out bool roomDeleted, out MultiplayerGameRoom? room);
    bool TryStartRoom(string roomId, out MultiplayerGameRoom room);
    WordGuessOutcome TrySubmitWord(string roomId, string connectionId, string word, out MultiplayerGameRoom? room);
    bool TryApplyTimeout(string roomId, DateTimeOffset nowUtc, out MultiplayerGameRoom room, out PlayerLifeLostDto? lifeLost);
    IReadOnlyCollection<MultiplayerGameRoom> GetRooms();
    IReadOnlyCollection<LobbyRoomDto> GetLobbySnapshot();
}
