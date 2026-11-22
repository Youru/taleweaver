namespace Taleweaver.Api.Features.Encounters;

/// <summary>What the GM reports or asks during an encounter. Each command returns the new game state.</summary>
public static class EncountersEndpoints
{
    public static IEndpointRouteBuilder MapEncountersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/encounter").WithTags("Encounters");
        NextTurn.Map(group);
        ApplyDamage.Map(group);
        SetEffect.Map(group);
        DefeatCombatant.Map(group);
        ActivateToken.Map(group);
        ReachEndCondition.Map(group);
        MoveFigure.Map(group);
        RevealArea.Map(group);
        CompleteAchievement.Map(group);
        MoveInitiativeCard.Map(group);
        return app;
    }
}
