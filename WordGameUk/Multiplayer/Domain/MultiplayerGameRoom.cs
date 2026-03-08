using WordGame.Multiplayer.Contracts;

namespace WordGame.Multiplayer.Domain;

public sealed class MultiplayerGameRoom
{
    private readonly object _sync = new();
    private readonly List<RoomPlayer> _players = [];
    private readonly Dictionary _dictionary;

    private int _currentTurnIndex = -1;
    private string _currentFragment = string.Empty;
    private DateTimeOffset _turnEndsAtUtc = DateTimeOffset.MinValue;
    private string _statusMessage = "Waiting for players.";
    private string _winnerConnectionId = string.Empty;
    private string _winnerName = string.Empty;

    public MultiplayerGameRoom(string id, string name, RoomSettings settings, Dictionary dictionary)
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

    public bool TryAddPlayer(string connectionId, string playerName)
    {
        lock (_sync)
        {
            if (_players.Any(x => x.ConnectionId == connectionId))
                return true;

            if (Status != GameRoomStatus.Waiting)
                return false;

            _players.Add(new RoomPlayer(connectionId, playerName, Settings.MaxLives));
            _statusMessage = $"Player '{playerName}' joined.";
            return true;
        }
    }

    public bool RemovePlayer(string connectionId)
    {
        lock (_sync)
        {
            var index = _players.FindIndex(x => x.ConnectionId == connectionId);
            if (index < 0)
                return false;

            var wasCurrentTurn = index == _currentTurnIndex;
            _players.RemoveAt(index);

            if (_players.Count == 0)
                return true;

            if (_currentTurnIndex >= _players.Count)
                _currentTurnIndex = 0;

            if (Status == GameRoomStatus.InProgress && wasCurrentTurn)
                MoveToNextTurn(Random.Shared);

            if (Status == GameRoomStatus.InProgress)
                CompleteIfWinnerExists();

            return true;
        }
    }

    public void RenamePlayer(string connectionId, string playerName)
    {
        lock (_sync)
        {
            var player = _players.FirstOrDefault(x => x.ConnectionId == connectionId);
            player?.Rename(playerName);
        }
    }

    public bool TryStart()
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.Waiting)
                return false;

            if (_players.Count < 2)
            {
                _statusMessage = "At least two players are required.";
                return false;
            }

            Status = GameRoomStatus.InProgress;
            _currentTurnIndex = -1;
            _winnerConnectionId = string.Empty;
            _winnerName = string.Empty;
            _statusMessage = "Game started.";
            MoveToNextTurn(Random.Shared);
            return true;
        }
    }

    public bool TrySubmitWord(string connectionId, string word)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.InProgress)
                return false;

            if (_currentTurnIndex < 0 || _currentTurnIndex >= _players.Count)
                return false;

            var currentPlayer = _players[_currentTurnIndex];
            if (currentPlayer.ConnectionId != connectionId)
                return false;

            var isCorrect = _dictionary.ContainsWord(word)
                && _dictionary.ContainsSyllable(word, _currentFragment);

            if (!isCorrect)
            {
                _statusMessage = $"Wrong guess by '{currentPlayer.Name}'.";
                return true;
            }

            _statusMessage = $"'{currentPlayer.Name}' guessed correctly.";
            MoveToNextTurn(Random.Shared);
            return true;
        }
    }

    public bool TryApplyTimeout(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (Status != GameRoomStatus.InProgress)
                return false;

            if (nowUtc < _turnEndsAtUtc)
                return false;

            if (_currentTurnIndex < 0 || _currentTurnIndex >= _players.Count)
                return false;

            var timedOutPlayer = _players[_currentTurnIndex];
            timedOutPlayer.LoseLife();

            if (timedOutPlayer.IsEliminated)
                _statusMessage = $"'{timedOutPlayer.Name}' timed out and was eliminated.";
            else
                _statusMessage = $"'{timedOutPlayer.Name}' timed out and lost a life.";

            CompleteIfWinnerExists();
            if (Status == GameRoomStatus.Finished)
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

    public RoomSnapshotDto ToSnapshot(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            var currentTurnConnectionId = _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count
                ? _players[_currentTurnIndex].ConnectionId
                : string.Empty;

            var players = _players
                .Select((player, index) => new RoomPlayerDto(
                    player.ConnectionId,
                    player.Name,
                    player.Lives,
                    player.IsEliminated,
                    index == _currentTurnIndex))
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
                Status == GameRoomStatus.Finished,
                _currentFragment,
                currentTurnConnectionId,
                secondsLeft,
                _winnerConnectionId,
                _winnerName,
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
                Status == GameRoomStatus.Finished,
                Settings.Difficulty,
                Settings.MaxLives);
        }
    }

    private void MoveToNextTurn(Random random)
    {
        if (_players.Count == 0)
            return;

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
        {
            Status = GameRoomStatus.Finished;
            _statusMessage = "No available fragments for selected difficulty.";
        }
    }

    private int FindNextAliveIndex(int currentIndex)
    {
        if (_players.Count == 0)
            return -1;

        for (var offset = 1; offset <= _players.Count; offset++)
        {
            var index = (currentIndex + offset + _players.Count) % _players.Count;
            if (!_players[index].IsEliminated)
                return index;
        }

        return -1;
    }

    private void CompleteIfWinnerExists()
    {
        var alivePlayers = _players.Where(x => !x.IsEliminated).ToArray();
        if (alivePlayers.Length != 1)
            return;

        var winner = alivePlayers[0];
        Status = GameRoomStatus.Finished;
        _winnerConnectionId = winner.ConnectionId;
        _winnerName = winner.Name;
        _statusMessage = $"Winner: {winner.Name}";
        _turnEndsAtUtc = DateTimeOffset.MinValue;
        _currentFragment = string.Empty;
        _currentTurnIndex = -1;
    }
}
