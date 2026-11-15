namespace Taleweaver.Api.Features.Scenarios;

public static class ScenariosEndpoints
{
    public static IServiceCollection AddScenariosFeature(this IServiceCollection services) =>
        services.AddScoped<ListScenarios.Handler>()
            .AddScoped<GetScenario.Handler>()
            .AddScoped<ValidateScenario.Handler>()
            .AddScoped<SaveScenario.Handler>()
            .AddScoped<DeleteScenario.Handler>();

    public static IEndpointRouteBuilder MapScenariosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenarios").WithTags("Scenarios");
        ListScenarios.Map(group);
        GetScenario.Map(group);
        ValidateScenario.Map(group);
        SaveScenario.Map(group);
        DeleteScenario.Map(group);
        return app;
    }
}
