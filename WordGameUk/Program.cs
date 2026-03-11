using Microsoft.AspNetCore.SignalR;
using WordGameUk.Application.Dictionary;
using WordGameUk.Components;
using WordGameUk.Components.Layout;
using WordGameUk.Infrastructure.Http;
using WordGameUk.Infrastructure.Realtime;
using WordGameUk.Multiplayer.Core.Multiplayer.Domain;
using WordGameUk.Multiplayer.Core.Multiplayer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(6);
    options.KeepAliveInterval = TimeSpan.FromSeconds(2);
});
builder.Services.AddSingleton(new WordDictionaryFileOptions(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "words.txt"),
    Path.Combine(builder.Environment.ContentRootPath, "Data", "words.filtered.txt")));
builder.Services.AddSingleton<IWordDictionaryService, WordDictionaryService>();
builder.Services.AddSingleton<IWordDictionary>(
    static sp => (IWordDictionary)sp.GetRequiredService<IWordDictionaryService>());
builder.Services.AddSingleton<IMultiplayerLobbyService, MultiplayerLobbyService>();
builder.Services.AddHostedService<MultiplayerRoomTickerService>();
builder.Services.AddScoped<RoomTopBarState>();
builder.Services.AddScoped<PlayerSessionStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<MultiplayerHub>("/hubs/multiplayer");
app.MapPost("/api/multiplayer/presence/disconnect",
        async (PresenceDisconnectRequest request, IMultiplayerLobbyService lobbyService, IHubContext<MultiplayerHub> hubContext) =>
        {
            var roomId = request.RoomId?.Trim() ?? string.Empty;
            var playerId = request.PlayerId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
                return Results.Ok();

            if (!lobbyService.TryLeaveRoomByPlayerId(roomId, playerId, out var roomDeleted, out var room))
                return Results.Ok();

            await hubContext.Clients.All.SendAsync("LobbyUpdated", lobbyService.GetLobbySnapshot());

            var groupName = $"room:{roomId}";
            if (roomDeleted)
            {
                await hubContext.Clients.Group(groupName).SendAsync("RoomClosed", roomId);
                return Results.Ok();
            }

            if (room is null)
                return Results.Ok();

            var snapshot = room.ToSnapshot(DateTimeOffset.UtcNow);
            await hubContext.Clients.Group(groupName).SendAsync("RoomUpdated", snapshot);
            return Results.Ok();
        })
    .DisableAntiforgery();

app.Run();
