using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>The custom dice of the enabled packs, by colour: what the duel helper needs to work out the odds.</summary>
public static class GetDice
{
    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<IReadOnlyDictionary<string, Die>> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            var dice = new Dictionary<string, Die>();
            foreach (var die in catalog.Packs.SelectMany(p => p.Dice))
            {
                dice[die.Id] = die;
            }

            return dice;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/dice", async Task<Ok<IReadOnlyDictionary<string, Die>>> (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("GetDice");
}
