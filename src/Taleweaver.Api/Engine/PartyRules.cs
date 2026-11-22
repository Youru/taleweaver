using System.Text.RegularExpressions;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The constraint engine of the departure (roadmap §5.2): may this adventurer come, take this kit, buy this with
/// that budget? Pure, and the same for the GM and the phones.
/// </summary>
public static partial class PartyRules
{
    public const int LargestParty = 6;

    public static class Errors
    {
        public static Error NotOffered(string name) => Error.Validation("Party.NotOffered", $"'{name}' may not set out in this scenario.");

        public static Error TooMany(int max) => Error.Conflict("Party.TooMany", $"The party has at most {max} adventurers.");

        public static Error UnknownKit(string kit) => Error.Validation("Party.UnknownKit", $"Kit '{kit}' is not for this adventurer.");

        public static Error ItemNotForSale(string item) => Error.Validation("Party.ItemNotForSale", $"'{item}' is not for sale at the departure.");

        public static Error TooManyItems(int max) => Error.Conflict("Party.TooManyItems", $"At most {max} cards are bought by each adventurer.");

        public static Error DisciplineNotOffered(string id) => Error.Validation("Party.DisciplineNotOffered", $"'{id}' cannot be learnt at the departure.");

        public static Error TooManyDisciplines(int max) => Error.Conflict("Party.TooManyDisciplines", $"At most {max} Disciplines are learnt at the departure.");

        public static Error MissingLevel(string id) => Error.Conflict("Party.MissingLevel", $"'{id}' needs a Discipline of each lower level of its tree.");

        public static Error NotEnoughXp(string name) => Error.Conflict("Party.NotEnoughXp", $"{name} has not enough XP.");

        public static readonly Error NotEnoughGold = Error.Conflict("Party.NotEnoughGold", "The party has not enough Gold.");

        public static readonly Error TooFew = Error.Conflict("Party.TooFew", "The party needs more adventurers.");

        public static readonly Error KitMissing = Error.Conflict("Party.KitMissing", "An adventurer still has to choose a kit.");

        public static Error Required(string name) => Error.Conflict("Party.Required", $"The story takes {name} along.");
    }

    /// <summary>A name goes into card and figure ids and into routes: none of ':', '/', ','.</summary>
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{M}\p{N} '’.\-]{0,39}$")]
    private static partial Regex CustomName();

    /// <summary>What the rules offer now, around the choices made.</summary>
    internal static void Describe(PartyPreparation preparation, Party? rules, Catalog catalog)
    {
        var start = rules?.Start;
        var cards = catalog.Packs.SelectMany(p => p.Adventurers).GroupBy(a => a.Id).Select(g => g.Last()).ToList();
        preparation.Adventurers = cards
            .Where(card => IsRequired(rules, card) || IsOffered(rules, card, preparation.HiddenShown))
            .Select(card => new AdventurerOffer(NameOf(card), card.Id, card.Image, card.Hidden, IsRequired(rules, card)))
            .ToList();
        preparation.Max = Math.Clamp(rules?.Size ?? LargestParty, 1, LargestParty);
        preparation.Min = Math.Clamp(Math.Max(rules?.MinSize ?? 1, preparation.Adventurers.Count(a => a.Required)), 1, preparation.Max);
        preparation.Gold = rules?.StartingGold ?? 0;
        preparation.Xp = rules?.StartingXp ?? 0;
        preparation.Custom = rules?.Custom ?? rules?.AllowedAdventurers is not { Count: > 0 };

        // A kit of an adventurer who is not offered would name them
        preparation.Kits = (start?.Kits ?? [])
            .Where(kit => kit.Adventurer is not { } who || preparation.Adventurers.Any(a => Same(a, who)))
            .ToList();

        preparation.ItemsOpen = start?.Items is not null;
        preparation.MaxItems = start?.Items?.Max;
        preparation.Items = start?.Items is not { } store
            ? []
            : catalog.Packs.SelectMany(p => p.Items).GroupBy(i => i.Id).Select(g => g.Last())
                .Where(i => i is { Cost: not null, Unique: false, Story: false } && i.Tier is { } tier && store.Tiers.Contains(tier)
                            && (store.Types is not { Count: > 0 } types || types.Contains(i.Type)))
                .Select(i => new ItemOffer(i.Id, i.Cost!.Value))
                .ToList();

        var learning = start?.Disciplines;
        preparation.MaxDisciplines = learning?.Max;
        preparation.Disciplines = preparation.Xp <= 0
            ? []
            : catalog.Packs.SelectMany(p => p.Disciplines).GroupBy(d => d.Id).Select(g => g.Last())
                .Where(d => d is { Story: false, Level: not null }
                            && (learning?.Trees is not { Count: > 0 } trees || trees.Contains(d.Tree))
                            && d.Level <= (learning?.MaxLevel ?? int.MaxValue))
                .Select(d => d.Id)
                .ToList();

        preparation.GoldLeft = preparation.Gold - GoldSpent(preparation);
        preparation.Missing = ToSetOut(preparation)?.Code;
    }

    /// <summary>The name the party will know: the offer's, or the one typed when the scenario allows it.</summary>
    public static Result<string> NameIn(PartyPreparation preparation, Catalog catalog, string? asked)
    {
        var name = asked?.Trim() ?? "";
        if (preparation.Adventurers.FirstOrDefault(a => Same(a, name)) is { } offer)
        {
            return offer.Name;
        }

        // A typed name is never a way around the list: not a card's, nor one of the inventory's owners
        var free = preparation.Custom && CustomName().IsMatch(name) && catalog.FindAdventurer(name) is null
            && !name.Equals(Session.StashOwner, StringComparison.OrdinalIgnoreCase)
            && !name.Equals(Session.UnassignedOwner, StringComparison.OrdinalIgnoreCase);
        if (!free)
        {
            return Errors.NotOffered(name);
        }

        return preparation.Choices.FirstOrDefault(c => string.Equals(c.Adventurer, name, StringComparison.OrdinalIgnoreCase))?.Adventurer ?? name;
    }

    /// <summary>What forbids these choices, or null. A party not complete yet is not forbidden: see <see cref="ToSetOut"/>.</summary>
    internal static Error? Check(PartyPreparation preparation, Catalog catalog)
    {
        if (preparation.Choices.Count > preparation.Max)
        {
            return Errors.TooMany(preparation.Max);
        }

        foreach (var choice in preparation.Choices)
        {
            if (!NameIn(preparation, catalog, choice.Adventurer).IsSuccess)
            {
                return Errors.NotOffered(choice.Adventurer);
            }

            var kit = choice.Kit is { } id ? KitsFor(preparation, choice.Adventurer).FirstOrDefault(k => k.Id == id) : null;
            if (choice.Kit is not null && kit is null)
            {
                return Errors.UnknownKit(choice.Kit);
            }

            var items = choice.Items ?? [];
            if (preparation.MaxItems is { } mostItems && items.Count > mostItems)
            {
                return Errors.TooManyItems(mostItems);
            }

            if (items.FirstOrDefault(i => preparation.Items.All(offer => offer.Item != i)) is { } notSold)
            {
                return Errors.ItemNotForSale(notSold);
            }

            if (CheckDisciplines(preparation, catalog, choice, kit) is { } refused)
            {
                return refused;
            }
        }

        return GoldSpent(preparation) > preparation.Gold ? Errors.NotEnoughGold : null;
    }

    private static Error? CheckDisciplines(PartyPreparation preparation, Catalog catalog, PartyChoice choice, StarterKit? kit)
    {
        var bought = choice.Disciplines ?? [];
        if (preparation.MaxDisciplines is { } most && bought.Count > most)
        {
            return Errors.TooManyDisciplines(most);
        }

        var known = new List<string>(kit?.Disciplines ?? []);
        var spent = 0;
        foreach (var id in InOrder(catalog, bought))
        {
            if (known.Contains(id) || !preparation.Disciplines.Contains(id))
            {
                return Errors.DisciplineNotOffered(id);
            }

            var card = catalog.FindDiscipline(id);
            if (DisciplineRules.MissingLevel(catalog, known, card) is not null)
            {
                return Errors.MissingLevel(id);
            }

            spent += DisciplineRules.Cost(catalog, known, card) ?? 0;
            known.Add(id);
        }

        return spent > preparation.Xp ? Errors.NotEnoughXp(choice.Adventurer) : null;
    }

    /// <summary>What is still missing to set out. A kit is owed only where nothing may be bought instead.</summary>
    internal static Error? ToSetOut(PartyPreparation preparation)
    {
        if (preparation.Choices.Count < preparation.Min)
        {
            return Errors.TooFew;
        }

        if (preparation.Adventurers.FirstOrDefault(a => a.Required && preparation.Choices.All(c => c.Adventurer != a.Name)) is { } absent)
        {
            return Errors.Required(absent.Name);
        }

        return !preparation.ItemsOpen && preparation.Choices.Any(c => c.Kit is null && KitsFor(preparation, c.Adventurer).Count > 0)
            ? Errors.KitMissing
            : null;
    }

    /// <summary>The kits an adventurer may take.</summary>
    internal static List<StarterKit> KitsFor(PartyPreparation preparation, string name)
    {
        var offer = preparation.Adventurers.FirstOrDefault(a => a.Name == name);
        return preparation.Kits.Where(kit => kit.Adventurer is not { } who || (offer is not null && Same(offer, who))).ToList();
    }

    /// <summary>The lower levels first: the order cards were picked in does not matter.</summary>
    internal static IEnumerable<string> InOrder(Catalog catalog, IEnumerable<string> disciplines) =>
        disciplines.OrderBy(id => catalog.FindDiscipline(id)?.Level ?? 0);

    private static int GoldSpent(PartyPreparation preparation) =>
        preparation.Choices.SelectMany(c => c.Items ?? []).Sum(id => preparation.Items.FirstOrDefault(i => i.Item == id)?.Price ?? 0);

    private static bool IsRequired(Party? rules, Adventurer card) => rules?.RequiredAdventurers?.Any(entry => Matches(card, entry)) == true;

    private static bool IsOffered(Party? rules, Adventurer card, bool hiddenShown)
    {
        if (rules?.ExcludedAdventurers?.Any(entry => Matches(card, entry)) == true)
        {
            return false;
        }

        if (rules?.AllowedAdventurers is not { Count: > 0 } allowed)
        {
            return hiddenShown || !card.Hidden;
        }

        var entries = allowed.Where(entry => Matches(card, entry)).ToList();
        return entries.Count > 0 && (hiddenShown || !card.Hidden || entries.Any(entry => !entry.EndsWith('*')));
    }

    private static string NameOf(Adventurer card) => card.Name.GetValueOrDefault("en") ?? card.Name.Values.FirstOrDefault() ?? card.Id;

    /// <summary>An entry of the scenario against a card: an id, a name, or "pack:adventurer/*".</summary>
    internal static bool Matches(Adventurer card, string entry) =>
        entry.EndsWith('*')
            ? card.Id.StartsWith(entry[..^1], StringComparison.Ordinal)
            : card.Id == entry || card.Name.Values.Any(name => string.Equals(name, entry, StringComparison.OrdinalIgnoreCase));

    private static bool Same(AdventurerOffer offer, string nameOrId) =>
        offer.Id == nameOrId || string.Equals(offer.Name, nameOrId, StringComparison.OrdinalIgnoreCase);
}
