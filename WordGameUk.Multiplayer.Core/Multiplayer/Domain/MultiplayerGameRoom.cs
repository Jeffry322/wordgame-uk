using WordGameUk.Multiplayer.Core.Multiplayer.Contracts;

namespace WordGameUk.Multiplayer.Core.Multiplayer.Domain;

public sealed class MultiplayerGameRoom
{
    private readonly object _sync = new();
    private readonly List<RoomPlayer> _players = [];
    private readonly Dictionary<string, int> _playerOrder = new(StringComparer.Ordinal);
    private readonly IWordDictionary _dictionary;
    private int _nextPlayerOrder;

    private int _currentTurnIndex = -1;
    private string _currentFragment = string.Empty;
    private DateTimeOffset _turnEndsAtUtc = DateTimeOffset.MinValue;
    private string _statusMessage = "Waiting for players.";

    public MultiplayerGameRoom(string id, string name, RoomSettings settings, IWordDictionary dictionary)
    {
        Id = id;
        Name = name;
        Settings = settings;
        _dictionary = dictionary;
        Status = GameRoomStatus.Waiting;
    }

    public string Id { get; }
    public string Name { get; }
    public RoomSettings Settings { get; }
    public GameRoomStatus Status { get; private set; }

    public JoinRoomResult TryJoinPlayer(string playerId, string connectionId, string playerName)
    {
        lock (_sync)
        {
            var existingPlayer = _players.FirstOrDefault(x => x.PlayerId == playerId);
            if (existingPlayer is not null)
            {
                if (existingPlayer.IsConnected)
                    return JoinRoomResult.AlreadyInRoom;

                existingPlayer.Connect(connectionId);
                existingPlayer.Rename(playerName);
                _statusMessage = $"Player '{existingPlayer.Name}' reconnected.";
                return JoinRoomResult.Reconnected;
            }

            var order = GetOrCreatePlayerOrder(playerId);
            AddPlayerByOrder(new RoomPlayer(playerId, connectionId, playerName, Settings.MaxLives, order));
            _statusMessage = Status == GameRoomStatus.InProgress
                ? $"Player '{playerName}' joined waiting list for next game."
                : $"Player '{playerName}' joined waiting list.";
            return JoinRoomResult.Joined;
        }
    }

    public bool TrySetPlayerInGame(string playerId, string connectionId, bool isInGame)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.Waiting)
                return false;

            var player = _players.FirstOrDefault(x => x.PlayerId == playerId);
            if (player is null || !player.IsConnected || player.ConnectionId != connectionId)
                return false;

            if (isInGame)
            {
                player.JoinGame(Settings.MaxLives);
                _statusMessage = $"Player '{player.Name}' joined game queue.";
            }
            else
            {
                player.LeaveGame(Settings.MaxLives);
                _statusMessage = $"Player '{player.Name}' moved to waiting list.";
            }

            return true;
        }
    }

    public bool RemoveOrDisconnectPlayerByConnection(string connectionId)
    {
        lock (_sync)
        {
            var index = _players.FindIndex(x => x.ConnectionId == connectionId);
            if (index < 0)
                return false;

            var disconnected = _players[index];
            disconnected.Disconnect(DateTimeOffset.UtcNow);
            if (Status == GameRoomStatus.Waiting)
                disconnected.LeaveGame(Settings.MaxLives);
            _statusMessage = $"Player '{disconnected.Name}' disconnected.";
            return true;
        }
    }

    public bool RemoveOrDisconnectPlayerByPlayerId(string playerId)
    {
        lock (_sync)
        {
            var index = _players.FindIndex(x => x.PlayerId == playerId);
            if (index < 0)
                return false;

            var disconnected = _players[index];
            disconnected.Disconnect(DateTimeOffset.UtcNow);
            if (Status == GameRoomStatus.Waiting)
                disconnected.LeaveGame(Settings.MaxLives);
            _statusMessage = $"Player '{disconnected.Name}' disconnected.";
            return true;
        }
    }

    public void RenamePlayer(string playerId, string playerName)
    {
        lock (_sync)
        {
            var player = _players.FirstOrDefault(x => x.PlayerId == playerId);
            player?.Rename(playerName);
        }
    }

    public bool TryStart()
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.Waiting)
                return false;

            var queuedPlayers = _players.Where(x => x.IsInGame && x.IsConnected).ToArray();
            if (queuedPlayers.Length < 2)
            {
                _statusMessage = "At least two players must join the game queue.";
                return false;
            }

            foreach (var player in queuedPlayers)
                player.JoinGame(Settings.MaxLives);

            Status = GameRoomStatus.InProgress;
            _currentTurnIndex = -1;
            _statusMessage = "Game started.";
            MoveToNextTurn(Random.Shared);
            return true;
        }
    }

    public WordGuessOutcome TrySubmitWord(string playerId, string connectionId, string word)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.InProgress)
                return WordGuessOutcome.Rejected;

            if (_currentTurnIndex < 0 || _currentTurnIndex >= _players.Count)
                return WordGuessOutcome.Rejected;

            var currentPlayer = _players[_currentTurnIndex];
            if (currentPlayer.PlayerId != playerId || currentPlayer.ConnectionId != connectionId)
                return WordGuessOutcome.Rejected;

            var isCorrect = _dictionary.ContainsWord(word)
                && _dictionary.ContainsSyllable(word, _currentFragment);

            if (!isCorrect)
            {
                _statusMessage = $"Wrong guess by '{currentPlayer.Name}'.";
                return WordGuessOutcome.Incorrect;
            }

            _statusMessage = $"'{currentPlayer.Name}' guessed correctly.";
            MoveToNextTurn(Random.Shared);
            return WordGuessOutcome.Correct;
        }
    }

    public bool CanPlayerGuess(string playerId, string connectionId)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.InProgress)
                return false;

            if (_currentTurnIndex < 0 || _currentTurnIndex >= _players.Count)
                return false;

            var currentPlayer = _players[_currentTurnIndex];
            return currentPlayer.PlayerId == playerId
                   && currentPlayer.ConnectionId == connectionId
                   && currentPlayer.IsConnected
                   && currentPlayer.IsInGame
                   && !currentPlayer.IsEliminated;
        }
    }

    public bool TryApplyTimeout(DateTimeOffset nowUtc, out PlayerLifeLostDto? lifeLost)
    {
        lock (_sync)
        {
            lifeLost = null;

            if (Status != GameRoomStatus.InProgress)
                return false;

            if (nowUtc < _turnEndsAtUtc)
                return false;

            if (_currentTurnIndex < 0 || _currentTurnIndex >= _players.Count)
                return false;

            var timedOutPlayer = _players[_currentTurnIndex];
            timedOutPlayer.LoseLife();
            lifeLost = new PlayerLifeLostDto(
                timedOutPlayer.PlayerId,
                timedOutPlayer.Lives,
                timedOutPlayer.IsEliminated);

            if (timedOutPlayer.IsEliminated)
                _statusMessage = $"'{timedOutPlayer.Name}' timed out and was eliminated.";
            else
                _statusMessage = $"'{timedOutPlayer.Name}' timed out and lost a life.";

            CompleteIfWinnerExists();
            if (Status != GameRoomStatus.InProgress)
                return true;

            MoveToNextTurn(Random.Shared);
            return true;
        }
    }

    public bool IsEmpty()
    {
        lock (_sync)
        {
            return _players.Count == 0;
        }
    }

    public bool PruneDisconnectedPlayers(DateTimeOffset nowUtc, TimeSpan disconnectGrace)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.Waiting)
                return false;

            var removedAny = _players.RemoveAll(player =>
                !player.IsConnected
                && player.DisconnectedAtUtc is not null
                && nowUtc - player.DisconnectedAtUtc.Value >= disconnectGrace) > 0;

            if (!removedAny)
                return false;

            if (_players.Count == 0)
            {
                ResetRoomState();
                return true;
            }

            _statusMessage = "Waiting for players.";
            return true;
        }
    }

    public RoomSnapshotDto ToSnapshot(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            var currentTurnPlayerId = _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count
                ? _players[_currentTurnIndex].PlayerId
                : string.Empty;

            var players = _players
                .Select((player, index) => new RoomPlayerDto(
                    player.PlayerId,
                    player.Name,
                    player.Lives,
                    player.IsEliminated,
                    player.IsConnected,
                    player.IsInGame,
                    index == _currentTurnIndex && player.IsInGame))
                .ToArray();

            var secondsLeft = 0;
            if (Status == GameRoomStatus.InProgress)
            {
                var remaining = (_turnEndsAtUtc - nowUtc).TotalSeconds;
                secondsLeft = Math.Max(0, (int)Math.Ceiling(remaining));
            }

            return new RoomSnapshotDto(
                Id,
                Name,
                Settings.Difficulty,
                Settings.RoundSeconds,
                Settings.MaxLives,
                Status == GameRoomStatus.InProgress,
                _currentFragment,
                currentTurnPlayerId,
                secondsLeft,
                players,
                _statusMessage);
        }
    }

    public LobbyRoomDto ToLobbyDto()
    {
        lock (_sync)
        {
            return new LobbyRoomDto(
                Id,
                Name,
                _players.Count,
                Status == GameRoomStatus.InProgress,
                Settings.Difficulty,
                Settings.MaxLives);
        }
    }

    private void MoveToNextTurn(Random random)
    {
        if (_players.Count == 0)
            return;

        if (_players.All(x => !x.IsInGame))
        {
            EndGameAndMoveToWaiting("No players are queued for the game.");
            return;
        }

        var nextIndex = FindNextAliveIndex(_currentTurnIndex);
        if (nextIndex < 0)
        {
            CompleteIfWinnerExists();
            return;
        }

        _currentTurnIndex = nextIndex;
        _currentFragment = _dictionary.GetRandomSyllable(random, Settings.Difficulty);
        _turnEndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(Settings.RoundSeconds);

        if (string.IsNullOrWhiteSpace(_currentFragment))
            EndGameAndMoveToWaiting("No available fragments for selected difficulty.");
    }

    private int FindNextAliveIndex(int currentIndex)
    {
        if (_players.Count == 0)
            return -1;

        for (var offset = 1; offset <= _players.Count; offset++)
        {
            var index = (currentIndex + offset + _players.Count) % _players.Count;
            if (_players[index].IsInGame && !_players[index].IsEliminated)
                return index;
        }

        return -1;
    }

    private void CompleteIfWinnerExists()
    {
        var alivePlayers = _players.Where(x => x.IsInGame && !x.IsEliminated).ToArray();
        if (alivePlayers.Length > 1)
            return;

        if (alivePlayers.Length == 1)
        {
            EndGameAndMoveToWaiting($"Winner: {alivePlayers[0].Name}");
            return;
        }

        EndGameAndMoveToWaiting("Game ended with no winner.");
    }

    private void RemovePlayerAt(int index)
    {
        var wasCurrentTurn = index == _currentTurnIndex;
        var removedName = _players[index].Name;
        _players.RemoveAt(index);

        if (_players.Count == 0)
        {
            ResetRoomState();
            return;
        }

        if (Status == GameRoomStatus.InProgress && index < _currentTurnIndex)
            _currentTurnIndex--;

        if (_currentTurnIndex >= _players.Count)
            _currentTurnIndex = _players.Count - 1;

        if (Status == GameRoomStatus.InProgress)
        {
            if (wasCurrentTurn)
                MoveToNextTurn(Random.Shared);

            CompleteIfWinnerExists();
            return;
        }

        _statusMessage = $"Player '{removedName}' left.";
    }

    private void ResetRoomState()
    {
        Status = GameRoomStatus.Waiting;
        _currentTurnIndex = -1;
        _currentFragment = string.Empty;
        _turnEndsAtUtc = DateTimeOffset.MinValue;
        _statusMessage = "Waiting for players.";
    }

    private void EndGameAndMoveToWaiting(string message)
    {
        Status = GameRoomStatus.Waiting;
        _currentTurnIndex = -1;
        _currentFragment = string.Empty;
        _turnEndsAtUtc = DateTimeOffset.MinValue;
        _statusMessage = message;

        foreach (var player in _players)
            player.LeaveGame(Settings.MaxLives);
    }

    private int GetOrCreatePlayerOrder(string playerId)
    {
        if (_playerOrder.TryGetValue(playerId, out var order))
            return order;

        order = _nextPlayerOrder++;
        _playerOrder[playerId] = order;
        return order;
    }

    private void AddPlayerByOrder(RoomPlayer player)
    {
        var index = _players.FindIndex(x => x.Order > player.Order);
        if (index < 0)
        {
            _players.Add(player);
            return;
        }

        _players.Insert(index, player);
    }
}
