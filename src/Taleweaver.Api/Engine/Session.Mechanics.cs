using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Mechanics of the encounters (play-spec lot 4): counters, the track dealt and arranged, effects for later,
/// Currents, the party split, the board kept from an encounter to the next.
/// </summary>
internal sealed partial class Session
{
    private const string AtRoundStart = "start";
    private const string AtRoundEnd = "end";
    private const string Front = "front";
    private const string Back = "back";

    private static class MechanicsErrors
    {
        public static Error UnknownCounter(string? id) => Error.NotFound("Counters.Unknown", $"No counter '{id}'.");

        public static readonly Error AlreadyStarted = Error.Conflict("Encounters.Started", "The track is dealt again at each round: only before the first.");

        public static Error NotSummonable(string? card) => Error.Validation("Encounters.NotSummonable", $"'{card}' is not an Encounter Esper of this summoner.");
    }

    private void DoMechanic(ScenarioAction action)
    {
        var id = ExtraString(action, "id");
        switch (action.Do)
        {
            case "setCounter" when id is not null:
                var counter = State.Campaign.Counters.FirstOrDefault(c => c.Id == id);
                var before = counter?.Value;
                if (counter is null)
                {
                    counter = new Counter { Id = id };
                    State.Campaign.Counters.Add(counter);
                }

                counter.Name = action.Text?.ToDictionary(n => n.Key, n => n.Value) ?? counter.Name;
                counter.On = Member(WhoIs(action)) is { } member ? AdventurerCard + member.Name : action.Token ?? action.Instance ?? counter.On;
                counter.Keep |= ExtraTrue(action, "keep");
                counter.Min = ExtraNumber(action, "min") ?? counter.Min;
                SetCounter(counter, action.Amount ?? before ?? 0, before);
                break;
            case "addCounter" when State.Campaign.Counters.FirstOrDefault(c => c.Id == id) is { } counted:
                SetCounter(counted, counted.Value + (action.Amount ?? 1), counted.Value);
                break;
            case "removeCounter":
                State.Campaign.Counters.RemoveAll(c => c.Id == id);
                break;
            case "drawAdventurers":
                var drawn = DrawAdventurers(action, Math.Max(1, action.Count ?? 1));
                SetVariable(ExtraString(action, "var") ?? "drawn", JsonSerializer.SerializeToElement(drawn));
                break;
            case "splitParty" when State.Encounter is not null:
                SplitParty(action);
                break;
            case "delay" when State.Encounter is { } encounter:
                var at = ExtraString(action, "at") ?? AtRoundEnd;
                encounter.Delayed.Add(new DelayedEffect
                {
                    Id = id ?? $"delay-{encounter.Delayed.Count + 1}",
                    Round = ExtraNumber(action, "round") ?? encounter.Round + (ExtraNumber(action, "rounds") ?? 1),
                    At = at == AtRoundStart ? AtRoundStart : AtRoundEnd,
                    Repeats = ExtraTrue(action, "repeat"),
                    Text = action.Text?.ToDictionary(n => n.Key, n => n.Value),
                    Actions = [.. action.Actions ?? []],
                });
                Log("delayed", action.Text, Args(("id", encounter.Delayed[^1].Id), ("round", Number(encounter.Delayed[^1].Round))));
                break;
            case "cancelDelay" when State.Encounter is { } running:
                running.Delayed.RemoveAll(d => d.Id == id);
                break;
            case "dealInitiative" when State.Encounter is { Round: 0 }:
                DealTrack();
                break;
            case "moveInitiative" when State.Encounter is not null:
                ArrangeTrack(action);
                break;
            case "addInitiativeCard" when State.Encounter is not null:
                AddInitiativeCard(action);
                break;
            case "removeInitiativeCard" when State.Encounter is not null && CardOf(action) is { } removed:
                Encounter.FixedCards?.Remove(removed);
                foreach (var figure in Encounter.Combatants)
                {
                    figure.InitiativeCards.Remove(removed);
                }

                RemoveFromTrack(removed);
                break;
            case "modifyFigure" when State.Encounter is not null:
                ModifyFigures(action);
                break;
            default:
                Log("manualAction", action.Text, Args(("do", action.Do), ("id", id)));
                break;
        }
    }

    // ---- Counters ----

    private Result CounterCommand(string? id, int by)
    {
        if (State.Campaign.Counters.FirstOrDefault(c => c.Id == id) is not { } counter)
        {
            return MechanicsErrors.UnknownCounter(id);
        }

        SetCounter(counter, counter.Value + by, counter.Value);
        return Result.Success();
    }

    /// <summary>A threshold crossed, either way, fires its events.</summary>
    private void SetCounter(Counter counter, int value, int? before)
    {
        counter.Value = counter.Min is { } floor ? Math.Max(floor, value) : value;
        var now = counter.Value;
        Log("counter", counter.Name, Args(("id", counter.Id), ("value", Number(now)), ("on", counter.On)));
        if (before is not { } was || was == now)
        {
            return;
        }

        Fire(e => e.Trigger.On == "counter" && e.Trigger.Counter == counter.Id
                  && (e.Trigger.Value is not { } at || (was < at && at <= now) || (now <= at && at < was)));
    }

    // ---- The track ----

    private Result DealCommand()
    {
        if (Encounter.Round > 0)
        {
            return MechanicsErrors.AlreadyStarted;
        }

        DealTrack();
        return Result.Success();
    }

    /// <summary>"Dealt out at the beginning of the Encounter" (p. 36): what is moved then holds for the first round.</summary>
    private void DealTrack()
    {
        ShuffleTrack();
        Encounter.TrackDealt = true;
        Log("initiativeDealt", args: Args(("track", string.Join(", ", Encounter.InitiativeTrack))));
    }

    /// <summary>moveInitiative: to the "front", the "back" or a place (1 first), or "amount" places (negative forward).</summary>
    private void ArrangeTrack(ScenarioAction action)
    {
        var encounter = Encounter;
        if (encounter.Round == 0 && !encounter.TrackDealt)
        {
            DealTrack();
        }

        if (action.Family is { } family && action.Instance is null)
        {
            // Every card of the family, keeping their order: the last one moves first toward the front or back by "amount"
            var track = encounter.InitiativeTrack;
            var cards = encounter.Combatants
                .Where(c => !c.Defeated && (c.Family == family || BaseFamilyOf(c) == family))
                .SelectMany(c => c.InitiativeCards).Distinct().Where(track.Contains).OrderBy(track.IndexOf).ToList();
            foreach (var each in action.To == Front || (action.To is null && (action.Amount ?? 0) > 0) ? Enumerable.Reverse(cards) : cards)
            {
                MoveTo(each, action.To, action.Amount ?? 0);
            }

            if (cards.Count > 0)
            {
                return;
            }
        }

        if (CardOf(action) is { } card && encounter.InitiativeTrack.Contains(card))
        {
            MoveTo(card, action.To, action.Amount ?? 0);
        }
        else
        {
            Log("manualAction", action.Text, Args(("do", action.Do), ("instance", action.Instance)));
        }
    }

    private void MoveTo(string card, string? to, int by)
    {
        var encounter = Encounter;
        var track = encounter.InitiativeTrack;
        var from = track.IndexOf(card);
        var target = to switch
        {
            Front => 0,
            Back => track.Count - 1,
            _ when int.TryParse(to, out var place) => place - 1,
            _ => from + by,
        };
        MoveInitiative(card, target - from);
        // Before anybody played this round, the front is who plays first
        if (encounter.PlayedCards.Count == 0 && encounter.CurrentTurn > 0)
        {
            encounter.CurrentTurn = 0;
        }
    }

    private string? CardOf(ScenarioAction action) =>
        ExtraString(action, "card")
        ?? (action.Instance is { } instance
            ? Find(instance)?.InitiativeCards.FirstOrDefault() ?? Encounter.Allies.FirstOrDefault(a => !a.Defeated && a.Id == instance)?.TrackCard
            : Member(WhoIs(action)) is { } member ? AdventurerCard + member.Name : null);

    /// <summary>One more card, one more turn (p. 69); "fixed": never shuffled.</summary>
    private void AddInitiativeCard(ScenarioAction action)
    {
        var encounter = Encounter;
        var named = ExtraString(action, "card");
        string? card = null;
        if (action.Instance is { } instance && Find(instance) is { } figure)
        {
            card = named ?? Copy((figure.InitiativeCards.FirstOrDefault() ?? "card:" + figure.Instance).Split('#')[0]);
            if (!figure.InitiativeCards.Contains(card))
            {
                figure.InitiativeCards.Add(card);
            }
        }
        else if (Member(WhoIs(action)) is { } member && ActiveParty.Contains(member))
        {
            // An adventurer's are told apart by their number
            card = Copy(AdventurerCard + member.Name);
        }
        else if (named is not null && encounter.InitiativeTrack.Contains(named))
        {
            card = named;
        }

        if (card is null)
        {
            Log("manualAction", action.Text, Args(("do", action.Do), ("instance", action.Instance)));
            return;
        }

        if (!encounter.InitiativeTrack.Contains(card))
        {
            AddToTrack([card]);
            Log("initiativeAdded", args: Args(("card", card)));
        }

        if (action.To is { } to)
        {
            MoveTo(card, to, 0);
        }

        if (ExtraTrue(action, "fixed"))
        {
            (encounter.FixedCards ??= [])[card] = action.To ?? Back;
        }
    }

    private string Copy(string card)
    {
        var track = Encounter.InitiativeTrack;
        if (!track.Contains(card) && !IsAdventurer(card))
        {
            return card;
        }

        var number = 2;
        while (track.Contains($"{card}#{number}"))
        {
            number++;
        }

        return $"{card}#{number}";
    }

    // ---- The party on the board ----

    private void SplitParty(ScenarioAction action)
    {
        var all = FogOfWar.MapsOnTable(EncounterNode, Encounter).SelectMany(m => m.Zones).ToList();
        var zones = (ExtraList(action, "zones") ?? all.Where(z => z.Kind == "startingZone").Select(z => z.Id).ToList())
            .Select(id => all.FirstOrDefault(z => z.Id == id)).OfType<Zone>().ToList();
        if (zones.Count == 0)
        {
            Log("manualAction", action.Text, Args(("do", action.Do)));
            return;
        }

        var left = Encounter.Adventurers.Where(a => !a.Defeated).ToList();
        for (var i = 0; left.Count > 0; i++)
        {
            var figure = left[NextRandom(left.Count)];
            left.Remove(figure);
            var zone = zones[i % zones.Count];
            var squares = RowByRow(zone.Rect).ToList();
            var rank = i / zones.Count;
            figure.Position = rank < squares.Count ? squares[rank] : null;
            Log("partySplit", args: Args(("adventurer", figure.Name), ("zone", zone.Id)));
        }
    }

    // ---- Later ----

    /// <summary>What was set for this round. "repeat": every round from then on, until cancelDelay.</summary>
    private void RunDelayed(string at)
    {
        var encounter = Encounter;
        var due = encounter.Delayed.Where(d => d.At == at && d.Round <= encounter.Round).ToList();
        foreach (var effect in due.Where(d => !d.Repeats))
        {
            encounter.Delayed.Remove(effect);
        }

        foreach (var effect in due)
        {
            Log("delayedDue", effect.Text, Args(("id", effect.Id)));
        }

        Push(due.SelectMany(d => d.Actions));
    }

    // ---- Turns: triggers, Currents, special Terrain ----

    private void FireTurn(string trigger, string figure)
    {
        var author = _author;
        _author = AuthorOf(figure);
        Fire(e => e.Trigger.On == trigger
                  && (e.Trigger.Instance is null || e.Trigger.Instance == figure)
                  && (e.Trigger.Family is null || Find(figure)?.Family == e.Trigger.Family));
        _author = author;
    }

    private void EndTurnOf(string figure)
    {
        var square = SquareOf(figure);
        var currents = CurrentsOnTable().ToList();
        if (currents.Count > 0)
        {
            // The squares are only followed if the GM wants: reminded even when this one reads as out of it
            var under = square is { } at ? currents.FirstOrDefault(c => InCurrent(c, at)) : null;
            Log("currentReminder", under?.Note, Args(("instance", figure), ("inside", under is null ? null : "true"),
                ("strength", under is null ? null : Number(under.Strength)), ("direction", under?.Direction)));
        }

        if (square is { } stand && TerrainAt(stand) is "special" or "dangerous")
        {
            Log("terrainReminder", args: Args(("instance", figure), ("terrain", TerrainAt(stand))));
        }

        FireTurn("turnEnd", figure);
    }

    private void RemindTokensInCurrents()
    {
        if (CurrentsOnTable().Any())
        {
            Log("currentTokensReminder");
        }
    }

    private IEnumerable<Current> CurrentsOnTable() => FogOfWar.MapsOnTable(EncounterNode, Encounter).SelectMany(m => m.Currents);

    private bool InCurrent(Current current, Cell at)
    {
        if (current.Rect is { } rect)
        {
            return rect.Cells().Contains(at);
        }

        return current.Tile is { } tile && FogOfWar.MapsOnTable(EncounterNode, Encounter).SelectMany(m => m.Tiles)
            .Any(t => t.Ref == tile && at.X >= t.X && at.X < t.X + Tile.SquaresPerSide && at.Y >= t.Y && at.Y < t.Y + Tile.SquaresPerSide);
    }

    // ---- The board kept ----

    private void KeepBoard(EncounterState encounter)
    {
        if (Scenario.Nodes.All(n => n.KeepBoardFrom != encounter.Node))
        {
            return;
        }

        State.Campaign.Boards.RemoveAll(b => b.From == encounter.Node);
        var board = new KeptBoard
        {
            From = encounter.Node,
            Combatants = Alive().ToList(),
            Waiting = [.. encounter.Waiting],
            Tokens = [.. encounter.Tokens],
            RevealedAreas = [.. encounter.RevealedAreas],
            PlacedDiagrams = [.. encounter.PlacedDiagrams],
            Adventurers = encounter.Adventurers.Where(a => !a.Defeated && a.Position is not null).ToList(),
            Allies = LivingAllies().Where(a => a.Position is not null).ToList(),
            Counters = [.. State.Campaign.Counters],
            Carried = [.. State.Campaign.Carried],
        };
        // A copy: the party and the counters live on
        State.Campaign.Boards.Add(JsonSerializer.Deserialize<KeptBoard>(JsonSerializer.Serialize(board, JsonDefaults.Options), JsonDefaults.Options)!);
    }

    private KeptBoard? BoardFor(Node node) =>
        node.KeepBoardFrom is { } from && State.Campaign.Boards.FirstOrDefault(b => b.From == from) is { } kept
            ? JsonSerializer.Deserialize<KeptBoard>(JsonSerializer.Serialize(kept, JsonDefaults.Options), JsonDefaults.Options)
            : null;

    private void LayKeptBoard(Node node, KeptBoard board)
    {
        var encounter = Encounter;
        var known = board.Tokens.Select(t => t.Id).ToHashSet();
        encounter.Tokens = [.. board.Tokens, .. encounter.Tokens.Where(t => !known.Contains(t.Id))];
        encounter.RevealedAreas = encounter.RevealedAreas.Union(board.RevealedAreas).ToList();
        encounter.PlacedDiagrams = board.PlacedDiagrams.Where(id => node.Diagrams.Any(d => d.Id == id)).ToList();
        encounter.Waiting.AddRange(board.Waiting);
        foreach (var figure in board.Combatants)
        {
            encounter.Combatants.Add(figure);
            AddToTrack(figure.InitiativeCards);
        }

        foreach (var counter in board.Counters.Where(c => State.Campaign.Counters.All(now => now.Id != c.Id)))
        {
            State.Campaign.Counters.Add(counter);
        }

        foreach (var thing in board.Carried.Where(c => State.Campaign.Carried.All(now => now.Id != c.Id)))
        {
            State.Campaign.Carried.Add(thing);
        }

        Log("boardKept", args: Args(("from", board.From)));
    }

    /// <summary>The setup may move the party; else each stays where they stood.</summary>
    private void StandWhereTheyWere(Node node, KeptBoard board)
    {
        if (node.Setup?.AdventurersStart is null)
        {
            foreach (var figure in Encounter.Adventurers)
            {
                figure.Position = board.Adventurers.FirstOrDefault(a => a.Name == figure.Name)?.Position ?? figure.Position;
            }
        }

        foreach (var ally in Encounter.Allies)
        {
            ally.Position ??= board.Allies.FirstOrDefault(a => a.Id == ally.Id)?.Position;
        }
    }
}
