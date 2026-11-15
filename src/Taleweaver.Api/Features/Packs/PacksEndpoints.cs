namespace Taleweaver.Api.Features.Packs;

public static class PacksEndpoints
{
    public static IServiceCollection AddPacksFeature(this IServiceCollection services) =>
        services.AddScoped<ListPacks.Handler>()
            .AddScoped<GetCombatant.Handler>()
            .AddScoped<SearchCatalog.Handler>();

    public static IEndpointRouteBuilder MapPacksEndpoints(this IEndpointRouteBuilder app)
    {
        var packs = app.MapGroup("/api/packs").WithTags("Packs");
        ListPacks.Map(packs);
        GetCombatant.Map(packs);
        SearchCatalog.Map(app.MapGroup("/api/catalog").WithTags("Packs"));
        return app;
    }
}
