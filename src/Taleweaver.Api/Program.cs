using FluentValidation;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Encounters;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Features.Scenarios;
using Taleweaver.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddPacksFeature().AddScenariosFeature().AddGamesFeature();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPacksEndpoints().MapScenariosEndpoints().MapGamesEndpoints().MapEncountersEndpoints();

app.Run();

public partial class Program;
