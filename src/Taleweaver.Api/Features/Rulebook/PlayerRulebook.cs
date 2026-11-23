using System.Text.RegularExpressions;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.Api.Features.Rulebook;

/// <summary>
/// The rulebook as a phone may read it (rules-spec §4): the chapters of rules only, without the examples and the
/// descriptions of the pictures (they show the story's cards), and without the name of a card the phones do not
/// see (<see cref="CardSight"/>). The same for every game: what a game reveals changes nothing here.
/// </summary>
public sealed partial class PlayerRulebook
{
    public const string Mask = "▒▒▒";

    /// <summary>A white list: a new chapter is a new decision.</summary>
    private static readonly HashSet<string> RuleChapters = ["04", "05", "06", "07", "08", "09", "10", "11"];

    private readonly Regex? _secret;

    public PlayerRulebook(Catalog catalog)
    {
        var names = SecretNames(catalog);
        // The longest first: "Cave Sickle Queen" before "Cave Sickle"
        var parts = names.OrderByDescending(n => n.Length)
            .Select(n => string.Join(@"[\s*_-]+", n.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape)));
        _secret = names.Count == 0
            ? null
            : new Regex($@"(?<!\p{{L}})(?:{string.Join('|', parts)})(?:e?s)?(?!\p{{L}})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public GetRulebook.Response For(GetRulebook.Response book) =>
        new([.. book.Chapters.Where(c => RuleChapters.Contains(c.Id)).Select(For)], Hide(book.Glossary));

    // A translation is the English chapter line for line: the lines the English leaves out go in every language
    private GetRulebook.Chapter For(GetRulebook.Chapter chapter)
    {
        var kept = KeptLines(chapter.Markdown);
        return chapter with
        {
            Markdown = Hide(Keep(chapter.Markdown, kept)),
            Translations = chapter.Translations?.ToDictionary(t => t.Key, t => Hide(Keep(t.Value, kept))),
        };
    }

    public RuleEntry For(RuleEntry entry) =>
        entry with { Name = Hide(entry.Name), Text = entry.Text is { } text ? Hide(text) : null };

    private Dictionary<string, string> Hide(LocalizedText text) => text.ToDictionary(t => t.Key, t => Hide(t.Value));

    // In lower case it is the word, not the card ("animate")
    private string Hide(string text) => _secret?.Replace(text, m => char.IsLower(m.Value[0]) ? m.Value : Mask) ?? text;

    /// <summary>What a phone may not read the name of. A name that a public piece bears too is public.</summary>
    public static IReadOnlySet<string> SecretNames(Catalog catalog)
    {
        var sight = new CardSight(catalog);
        var secret = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var open = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(bool seen, LocalizedText? name) => (seen ? open : secret).UnionWith(name?.Values ?? []);

        foreach (var pack in catalog.Packs)
        {
            foreach (var adventurer in pack.Adventurers)
            {
                Add(sight.Sees(adventurer), adventurer.Name);
                Add(sight.Sees(adventurer), adventurer.FullName);
            }

            foreach (var item in pack.Items)
            {
                Add(sight.Sees(item), item.Name);
            }

            foreach (var discipline in pack.Disciplines)
            {
                Add(sight.Sees(discipline), discipline.Name);
            }

            foreach (var combatant in pack.Combatants)
            {
                Add(false, combatant.Name);
                secret.Add(combatant.Family.Replace('-', ' '));
            }

            // The pieces of the box and the words of the rules
            foreach (var token in pack.Tokens)
            {
                Add(true, token.Name);
            }

            if (pack.Rules is { } rules)
            {
                foreach (var entry in rules.Effects.Concat(rules.AbilityTokens).Concat(rules.TerrainTypes).Concat(rules.Tags).Concat(rules.CombatantTokens))
                {
                    Add(true, entry.Name);
                }
            }
        }

        secret.ExceptWith(open);
        secret.RemoveWhere(n => n.Trim().Length < 3);
        return secret;
    }

    private static string[] Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

    private static string Keep(string markdown, HashSet<int> kept) =>
        string.Join('\n', Lines(markdown).Where((_, i) => kept.Contains(i)));

    /// <summary>The lines left once the examples and the descriptions of the pictures are out.</summary>
    private static HashSet<int> KeptLines(string markdown)
    {
        var kept = new HashSet<int>();
        var leftOut = 0; // the level of the heading being left out
        var lines = Lines(markdown);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Heading().Match(line) is { Success: true } heading)
            {
                var level = heading.Groups[1].Length;
                if (leftOut > 0 && level <= leftOut)
                {
                    leftOut = 0;
                }

                if (leftOut == 0 && ExampleTitle().IsMatch(line))
                {
                    leftOut = level;
                }
            }

            // The page marks stay: what follows keeps its page
            if (line.StartsWith("<!-- p.", StringComparison.Ordinal)
                || (leftOut == 0 && !line.StartsWith('>') && !ExampleLine().IsMatch(line)))
            {
                kept.Add(i);
            }
        }

        return kept;
    }

    [GeneratedRegex("^(#{1,4}) ")]
    private static partial Regex Heading();

    [GeneratedRegex("Example|Walkthrough", RegexOptions.IgnoreCase)]
    private static partial Regex ExampleTitle();

    [GeneratedRegex(@"^[\W_]*(?:worked |movement |line of sight )?example", RegexOptions.IgnoreCase)]
    private static partial Regex ExampleLine();
}
