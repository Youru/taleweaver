using FluentValidation;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Allies;
using Taleweaver.Api.Features.Assets;
using Taleweaver.Api.Features.Encounters;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Items;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Features.Party;
using Taleweaver.Api.Features.Play;
using Taleweaver.Api.Features.Preparation;
using Taleweaver.Api.Features.Print;
using Taleweaver.Api.Features.Rooms;
using Taleweaver.Api.Features.Rulebook;
using Taleweaver.Api.Features.Scenarios;
using Taleweaver.Api.Hosting;
using Taleweaver.Api.Storage;

// Given away, the app carries its content and its settings (PackagedApp); from the repository, appsettings.json
PackagedApp? packaged = null;
if (PackagedApp.IsPackaged)
{
    packaged = PackagedApp.Start();
    if (packaged is null)
    {
        return;
    }
}

var builder = packaged?.CreateBuilder() ?? WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ContentOptions>().BindConfiguration(ContentOptions.Section);
builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddSingleton(TimeProvider.System);

// Storage: JSON files today; replace these lines to move to a database.
builder.Services.AddSingleton<IContentStore, JsonContentStore>();
builder.Services.AddSingleton<IGameStore, JsonGameStore>();
builder.Services.AddSingleton<CatalogProvider>();

builder.Services.AddPacksFeature().AddScenariosFeature().AddGamesFeature().AddAssetsFeature().AddPrintFeature().AddRoomsFeature();

var app = builder.Build();

app.UseExceptionHandler();
if (packaged is not null)
{
    app.UseHostGuard();
}

app.UseWebFront();
app.UseOriginGuard();
app.UseRoomGuard();
app.UseWebSockets();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPacksEndpoints().MapScenariosEndpoints().MapGamesEndpoints().MapEncountersEndpoints().MapPlayEndpoints().MapItemsEndpoints().MapPartyEndpoints().MapPreparationEndpoints().MapAlliesEndpoints().MapPrintEndpoints().MapRoomEndpoints().MapRulebookEndpoints();

// Built now so that it listens to the rooms from the start.
app.Services.GetRequiredService<RoomNotifier>();
GetAsset.Map(app);

// Where the phones will join: the GM sees at once whether the detected address is the right one.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = Network.LanAddresses();
    var port = Environment.GetEnvironmentVariable("TALEWEAVER_WEB_PORT") ?? "5173";
    app.Logger.LogInformation(
        addresses.Count > 0
            ? "Room: phones join at {Urls} (first one in the QR code; set {Variable} to force another address)"
            : "Room: no Wi-Fi address found, phones cannot join ({Urls}); set {Variable} to give one",
        string.Join(", ", addresses.Select(a => $"http://{a}:{port}/join")),
        Network.HostVariable);
});

if (packaged is not null)
{
    packaged.Run(app);
}
else
{
    app.Run();
}

public partial class Program;
