using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>
/// The Adventure Sheet at the end of an Act (roadmap §5.2 ter): what the next Act may take over. What it does take
/// over will be that Act's rules; the file only carries all of it.
/// </summary>
public static class ExportAct
{
    public const string Format = "taleweaver.actExport";

    public sealed record Sheet(
        string Format,
        int SchemaVersion,
        string Scenario,
        string? Ending,
        DateTimeOffset EndedAt,
        IReadOnlyList<string> EnabledPacks,
        GameOptions? Options,
        CampaignState Campaign);

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id}/act-export", async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
                string id, GameRunner runner, CancellationToken cancellationToken) =>
            {
                var loaded = await runner.LoadAsync(id, cancellationToken);
                if (!loaded.IsSuccess)
                {
                    return loaded.Error!.ToProblem();
                }

                var game = loaded.Value;
                if (game.State.Status != GameStatus.Finished)
                {
                    return GameRunner.Errors.NotFinished.ToProblem();
                }

                var sheet = new Sheet(Format, 1, game.Scenario, game.State.Ending, game.UpdatedAt, game.EnabledPacks, game.Options, game.State.Campaign);
                var name = $"{game.Scenario.Split('/')[^1]}-end-{game.UpdatedAt:yyyyMMdd-HHmm}.json";
                return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(sheet, JsonDefaults.FileOptions), "application/json", name);
            })
            .WithName("ExportAct");
}
