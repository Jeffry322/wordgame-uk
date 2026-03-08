
using WordGame;
using WordGame.Multiplayer;
using WordGame.Multiplayer.Services;
using WordGameUk.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<Dictionary>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IMultiplayerLobbyService, MultiplayerLobbyService>();
builder.Services.AddHostedService<MultiplayerRoomTickerService>();

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

app.Run();