namespace Taleweaver.Api.Features.Packs;

public static class PacksEndpoints
{
    public static IServiceCollection AddPacksFeature(this IServiceCollection services) =>
        services.AddScoped<ListPacks.Handler>()
            .AddScoped<GetCombatant.Handler>()
            .AddScoped<SearchCatalog.Handler>()
            .AddScoped<GetBoardPieces.Handler>()
            .AddScoped<GetDice.Handler>()
            .AddScoped<GetEffects.Handler>()
            .AddScoped<GetInitiativeCards.Handler>()
            .AddScoped<GetItems.Handler>();

    public static IEndpointRouteBuilder MapPacksEndpoints(this IEndpointRouteBuilder app)
    {
        var packs = app.MapGroup("/api/packs").WithTags("Packs");
        ListPacks.Map(packs);
        GetCombatant.Map(packs);
        var catalog = app.MapGroup("/api/catalog").WithTags("Packs");
        SearchCatalog.Map(catalog);
        GetBoardPieces.Map(catalog);
        GetDice.Map(catalog);
        GetEffects.Map(catalog);
        GetReference.Map(catalog);
        GetInitiativeCards.Map(catalog);
        GetItems.Map(catalog);
        GetAllies.Map(catalog);
        GetUpgradesAndDisciplines.Map(catalog);
        return app;
    }
}
