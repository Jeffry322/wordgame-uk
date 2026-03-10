using WordGameUk;
using WordGameUk.Components;
using WordGameUk.Components.Layout;
using WordGameUk.Multiplayer;
using WordGameUk.Multiplayer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<Dictionary>();
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(12);
    options.KeepAliveInterval = TimeSpan.FromSeconds(4);
});
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

app.Run();
