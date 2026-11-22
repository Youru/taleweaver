using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>What a figure carries (play-spec A7): an idol, a wounded friend. Put down where the carrier falls.</summary>
internal sealed partial class Session
{
    private static class CarriedErrors
    {
        public static Error Unknown(string? id) => Error.NotFound("Carried.Unknown", $"Nothing called '{id}' is carried or lying around.");
    }

    private void DoCarried(ScenarioAction action)
    {
        var id = ExtraString(action, "id") ?? action.Ref ?? "";
        var carried = State.Campaign.Carried.FirstOrDefault(c => c.Id == id);
        switch (action.Do)
        {
            case "carry":
                var who = action.Instance ?? (MemberOf(WhoIs(action) ?? _author) is { } member ? AdventurerCard + member.Name : null);
                if (who is null)
                {
                    Push([action with { Who = System.Text.Json.JsonSerializer.SerializeToElement(AskRole) }]);
                    return;
                }

                if (carried is null)
                {
                    carried = new CarriedThing { Id = id };
                    State.Campaign.Carried.Add(carried);
                }

                carried.Name = action.Text?.ToDictionary(n => n.Key, n => n.Value) ?? carried.Name;
                carried.Keep |= ExtraTrue(action, "keep");
                if (action.Extra?.TryGetValue("lostOnDangerous", out var lost) == true)
                {
                    carried.SurvivesDangerous = lost.ValueKind == System.Text.Json.JsonValueKind.False;
                }

                // What lay on the board as a token is now on the figure
                if (action.Token is { } tokenId && State.Encounter?.Tokens.FirstOrDefault(t => !t.Removed && (t.Id == tokenId || t.Key == tokenId)) is { } token)
                {
                    (carried.TokenType, carried.TokenKey, carried.Color) = (token.Type, token.Key, token.Color);
                    RemoveToken(token.Id);
                }

                PickUp(carried, who);
                break;
            case "dropCarried" when carried is not null:
                PutDown(carried, Locate(action.At) ?? SquareOf(carried.Carrier));
                break;
            case "loseCarried" when carried is not null:
                Lose(carried);
                break;
        }
    }

    /// <summary>The GM reports it: picked up or passed on, put down (nobody named), or lost.</summary>
    private Result CarryCommand(string? id, string? figure, string? effect)
    {
        if (State.Campaign.Carried.FirstOrDefault(c => c.Id == id) is not { } carried)
        {
            return CarriedErrors.Unknown(id);
        }

        if (effect == "lose")
        {
            Lose(carried);
        }
        else if (figure is null)
        {
            PutDown(carried, SquareOf(carried.Carrier));
        }
        else
        {
            var known = IsAdventurer(figure) ? MemberOf(figure) is { Defeated: false }
                : State.Encounter is not null && (IsAlly(figure) ? FindAlly(figure) is not null : Find(figure) is { Defeated: false });
            if (!known)
            {
                return GameEngine.Errors.UnknownFigure(figure);
            }

            PickUp(carried, figure);
        }

        return Result.Success();
    }

    private void PickUp(CarriedThing carried, string figure)
    {
        TakeOffBoard(carried);
        (carried.Carrier, carried.Position, carried.Lost) = (figure, null, false);
        Log("carriedTaken", carried.Name, Args(("id", carried.Id), ("figure", figure)));
    }

    private void PutDown(CarriedThing carried, Cell? at)
    {
        if (at is { } square && !carried.SurvivesDangerous && TerrainAt(square) == "dangerous")
        {
            Lose(carried);
            return;
        }

        TakeOffBoard(carried);
        (carried.Carrier, carried.Position) = (null, at);
        if (at is { } on && State.Encounter is { } encounter && carried.TokenKey is { } key)
        {
            encounter.Tokens.Add(new BoardToken { Id = CarriedTokenId(carried), Key = key, Type = carried.TokenType, Color = carried.Color, Area = new Rect(on.X, on.Y) });
        }

        Log("carriedDropped", carried.Name, Args(("id", carried.Id), ("at", at?.ToString())));
    }

    private void Lose(CarriedThing carried)
    {
        TakeOffBoard(carried);
        (carried.Carrier, carried.Position, carried.Lost) = (null, null, true);
        Log("carriedLost", carried.Name, Args(("id", carried.Id)));
    }

    private void TakeOffBoard(CarriedThing carried) => State.Encounter?.Tokens.RemoveAll(t => t.Id == CarriedTokenId(carried));

    private static string CarriedTokenId(CarriedThing carried) => "carried-" + carried.Id;

    /// <summary>The carrier leaves the board: to call while its square is still known.</summary>
    private void DropCarriedBy(string figure, Cell? at)
    {
        foreach (var carried in State.Campaign.Carried.Where(c => c.Carrier == figure).ToList())
        {
            PutDown(carried, at);
        }
    }

    /// <summary>Into the next encounter goes only what is kept and held by the party.</summary>
    private void LeaveCarriedBehind() =>
        State.Campaign.Carried.RemoveAll(c => !c.Lost && !(c.Keep && IsAdventurer(c.Carrier)));

    private Cell? SquareOf(string? figure) =>
        State.Encounter is not { } encounter || figure is null ? null
        : IsAdventurer(figure) ? encounter.Adventurers.FirstOrDefault(a => a.Name == AdventurerOfCard(figure))?.Position
        : IsAlly(figure) ? FindAlly(figure)?.Position
        : Find(figure)?.Position;

    /// <summary>The Terrain of a square: a piece lying on it (a Plank is Normal), else what the tile prints.</summary>
    private string? TerrainAt(Cell square)
    {
        if (State.Encounter is not { } encounter)
        {
            return null;
        }

        foreach (var token in encounter.Tokens.AsEnumerable().Reverse().Where(t => !t.Removed && t.Type is not null && t.Area is { } area && area.Cells().Contains(square)))
        {
            var type = TokenType(token.Type!);
            if (type.StartsWith("plank", StringComparison.Ordinal))
            {
                return "normal";
            }

            if (IsTerrain(token.Type))
            {
                return Catalog.FindToken(token.Type)?.Terrain ?? type.Split('-')[0];
            }
        }

        return FogOfWar.MapsOnTable(EncounterNode, encounter).SelectMany(m => m.Tiles).Reverse()
            .SelectMany(laid => Catalog.FindTile(laid.Ref)?.PrintedTerrain(laid) ?? [])
            .Where(t => t.Cell == square)
            .Select(t => t.Kind)
            .FirstOrDefault();
    }
}
