namespace Taleweaver.Api.Content;

/// <summary>The "party" block (roadmap §5.2): rules that can be followed, cards that exist.</summary>
internal static class PartyChecker
{
    private const string Where = "party";

    public static (List<ScenarioIssue> Errors, List<ScenarioIssue> Warnings) Check(Party? party, Catalog catalog)
    {
        var errors = new List<ScenarioIssue>();
        var warnings = new List<ScenarioIssue>();
        if (party is null)
        {
            return (errors, warnings);
        }

        void Error(string code, string message) => errors.Add(new ScenarioIssue($"Scenarios.{code}", Where, message));
        void Warn(string code, string message) => warnings.Add(new ScenarioIssue($"Scenarios.{code}", Where, message));

        var most = party.Size ?? Engine.PartyRules.LargestParty;
        var required = party.RequiredAdventurers ?? [];
        if (most is < 1 or > Engine.PartyRules.LargestParty || party.MinSize < 1 || party.MinSize > most || required.Count > most)
        {
            Error("PartySize", $"a party has 1 to {Engine.PartyRules.LargestParty} adventurers: size {most}, minSize {party.MinSize ?? 1}, {required.Count} required");
        }

        if (party.StartingGold < 0 || party.StartingXp < 0)
        {
            Error("PartyBudget", "startingGold and startingXp cannot be negative");
        }

        var cards = catalog.Packs.SelectMany(p => p.Adventurers).ToList();
        bool Known(string entry) => cards.Count == 0 || cards.Any(card => Engine.PartyRules.Matches(card, entry));
        foreach (var entry in required.Where(e => !Known(e) || e.EndsWith('*')))
        {
            Error("UnknownAdventurer", $"required adventurer '{entry}' is not in the catalogue");
        }

        foreach (var entry in (party.AllowedAdventurers ?? []).Concat(party.ExcludedAdventurers ?? []).Where(e => !Known(e)))
        {
            Warn("UnknownAdventurer", $"adventurer '{entry}' is not in the catalogue");
        }

        var kits = party.Start?.Kits ?? [];
        foreach (var twice in kits.GroupBy(k => k.Id).Where(g => g.Count() > 1))
        {
            Error("DuplicateKit", $"kit '{twice.Key}' is declared {twice.Count()} times");
        }

        foreach (var kit in kits)
        {
            if (kit.Adventurer is { } who && !Known(who))
            {
                Warn("UnknownAdventurer", $"kit '{kit.Id}': adventurer '{who}' is not in the catalogue");
            }

            foreach (var id in kit.Items.Where(id => catalog.FindItem(id) is null).Concat(kit.Disciplines.Where(id => catalog.FindDiscipline(id) is null)))
            {
                Error("UnknownKitCard", $"kit '{kit.Id}': '{id}' is not in the catalogue");
            }
        }

        foreach (var tier in party.Start?.Items?.Tiers.Where(t => !LootLevels.IsKnown(t)) ?? [])
        {
            Error("UnknownTier", $"start.items: unknown tier '{tier}'");
        }

        if (party.Start?.Items is { Tiers.Count: 0 })
        {
            Error("UnknownTier", "start.items: name at least one tier");
        }

        return (errors, warnings);
    }
}
