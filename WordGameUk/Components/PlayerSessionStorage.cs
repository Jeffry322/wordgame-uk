using Microsoft.JSInterop;

namespace WordGameUk.Components;

public sealed class PlayerSessionStorage
{
    private const string PlayerIdStorageKey = "wordgame.playerId";
    private const string DisplayNameStorageKey = "wordgame.displayName";
    private readonly IJSRuntime _js;

    public PlayerSessionStorage(IJSRuntime js)
    {
        _js = js;
    }

    public Task<string?> GetDisplayNameAsync()
    {
        return _js.InvokeAsync<string?>("localStorage.getItem", DisplayNameStorageKey).AsTask();
    }

    public async Task<string> GetOrCreatePlayerIdAsync()
    {
        var storedId = await _js.InvokeAsync<string?>("localStorage.getItem", PlayerIdStorageKey);
        if (!string.IsNullOrWhiteSpace(storedId))
            return storedId;

        var createdId = Guid.NewGuid().ToString("N");
        await _js.InvokeVoidAsync("localStorage.setItem", PlayerIdStorageKey, createdId);
        return createdId;
    }

    public Task SetDisplayNameAsync(string displayName)
    {
        return _js.InvokeVoidAsync("localStorage.setItem", DisplayNameStorageKey, displayName).AsTask();
    }

    public static string NormalizeDisplayName(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Player" : trimmed;
    }
}
