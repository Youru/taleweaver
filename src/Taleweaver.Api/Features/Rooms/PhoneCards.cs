using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// What the phones may read of the catalogue and of the assets (room-spec §8), a white list: what is public in the
/// box, and the secret cards only once revealed, held or in view. A phone asks for its pictures without a token, so
/// the list is the same for the phones of every open room.
/// </summary>
public sealed class CardSight
{
    /// <summary>The app's own decor: blank templates and the rulebook's art.</summary>
    private static readonly string[] Decor = ["assets/templates/", "assets/rulebook/art/"];

    private readonly Catalog _catalog;
    private readonly HashSet<string> _mapped;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _families = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pictures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<HashSet<string>> _readable;

    public CardSight(Catalog catalog)
    {
        _catalog = catalog;
        _mapped = catalog.Packs.SelectMany(p => p.Manifest.HiddenCards.Values).SelectMany(ids => ids).ToHashSet();
        _readable = new(ReadablePictures);
    }

    /// <summary>
    /// Adds what a game shows its table. <paramref name="table"/> is projected for the whole party and without a
    /// seat: the fog is cut, and nothing a player typed in a request is in it.
    /// </summary>
    public void Add(GameState state, PlayerView table)
    {
        foreach (var value in state.Campaign.RevealedHiddenCards.SelectMany(_catalog.HiddenCardValues))
        {
            SeeCard(value);
        }

        foreach (var member in state.Campaign.Party)
        {
            _names.Add(member.Name);
            SeeAll(member.Equipped.Concat(member.Pack).Select(i => i.Item));
            SeeAll(member.Disciplines.Select(d => d.Id));
        }

        SeeAll(table.Stash.Concat(table.Unassigned).Select(i => i.Item));
        SeeAll(table.Shop?.Select(s => s.Item) ?? []);
        if (table.Preparation is { } preparation)
        {
            _names.UnionWith(preparation.Adventurers.Select(a => a.Name));
            SeeAll(preparation.Items.Select(i => i.Item));
            SeeAll(preparation.Disciplines);
            SeeAll(preparation.Kits.SelectMany(k => k.Items));
        }

        if (table.Encounter is { } encounter)
        {
            SeeAll(encounter.InitiativeTrack.Select(c => c.StartsWith("ally:", StringComparison.Ordinal) ? c["ally:".Length..] : c));
            foreach (var combatant in encounter.Combatants)
            {
                SeeAll(combatant.InitiativeCards);
                _families.Add(combatant.Family);
                Show(combatant.Image);
            }

            foreach (var ally in encounter.Allies)
            {
                _families.Add(ally.Family);
                Show(ally.Image);
            }

            foreach (var card in encounter.AllyOptions.SelectMany(o => o.Cards))
            {
                Show(card.Image);
            }

            if (encounter.Board is { } board)
            {
                _families.UnionWith(board.Figures.Concat(board.Allies).Select(f => f.Figure).OfType<string>());
                foreach (var token in board.Tokens)
                {
                    Show(token.Image);
                }
            }
        }
    }

    /// <summary>A picture the GM showed the players (room-spec §7.5).</summary>
    public void Show(string? picture)
    {
        if (picture is not null)
        {
            _pictures.Add(picture);
        }
    }

    /// <summary>Monster Loot is secret too: its name tells the monster.</summary>
    public bool Sees(Item item) => _seen.Contains(item.Id) || !(item.Story || item.Tier == "monster" || _mapped.Contains(item.Id));

    public bool Sees(Discipline discipline) => _seen.Contains(discipline.Id) || !(discipline.Story || _mapped.Contains(discipline.Id));

    public bool Sees(Adventurer adventurer) => !adventurer.Hidden || adventurer.Name.Values.Any(_names.Contains);

    /// <summary>An opponent's card only in view; an adventurer's with the adventurer.</summary>
    public bool Sees(InitiativeCard card) =>
        _seen.Contains(card.Id) || (card.Adventurer && AdventurerNames().Contains(card.Label, StringComparer.OrdinalIgnoreCase));

    /// <summary>A combatant family in view: never by an adventurer's name (some share one).</summary>
    public bool SeesFamily(string family) => _families.Contains(family);

    /// <summary>The standee of a figure key: the family's miniature only with the family.</summary>
    public string? Standee(string key, string? image) =>
        SeesFamily(key) ? image : _catalog.Packs.Select(p => p.Manifest.Figures.GetValueOrDefault(key)?.Image).FirstOrDefault(i => i is not null);

    /// <summary>The key of a figure: a family in view, or an adventurer's name ("rook").</summary>
    public bool SeesFigure(string key) =>
        _families.Contains(key) || AdventurerNames().Any(n => string.Equals(n.Replace(' ', '-'), key, StringComparison.OrdinalIgnoreCase));

    /// <summary><paramref name="path"/> from the repository root ("assets/act1/tiles/um01.jpg").</summary>
    public bool SeesPicture(string path) =>
        Decor.Any(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase)) || _readable.Value.Contains(path);

    /// <summary>Changes when a phone has something new to read: it then reads the catalogue again.</summary>
    public string Fingerprint()
    {
        var all = _seen.Concat(_families).Concat(_names).Order(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', all))))[..12];
    }

    private void SeeCard(string value)
    {
        if (value.StartsWith("assets/", StringComparison.Ordinal))
        {
            Show(value);
            return;
        }

        _seen.Add(value);
        if (_catalog.FindCombatant(value) is { } card)
        {
            _families.Add(card.Family);
            SeeAll(card.InitiativeCards);
            Show(card.Image);
            Show(card.ImageBack);
        }
    }

    private void SeeAll(IEnumerable<string?> ids) => _seen.UnionWith(ids.OfType<string>());

    private IEnumerable<string> AdventurerNames() =>
        _catalog.Packs.SelectMany(p => p.Adventurers).Where(Sees).SelectMany(a => a.Name.Values);

    private HashSet<string> ReadablePictures()
    {
        var readable = new HashSet<string>(_pictures, StringComparer.OrdinalIgnoreCase);
        void Read(params IEnumerable<string?> paths) => readable.UnionWith(paths.OfType<string>());

        foreach (var pack in _catalog.Packs)
        {
            foreach (var item in pack.Items.Where(Sees))
            {
                Read(item.Image, item.ImageBack);
            }

            foreach (var adventurer in pack.Adventurers.Where(Sees))
            {
                Read(adventurer.Image, adventurer.ImageBack);
            }

            Read(pack.Disciplines.Where(Sees).Select(d => d.Image));
            Read(pack.InitiativeCards.Where(Sees).Select(c => c.Image));
            Read(pack.ItemUpgrades.Select(u => u.Image));
            Read(pack.Tiles.Select(t => t.Image));
            foreach (var token in pack.Tokens)
            {
                Read(token.Image);
                Read(token.ImageVariants);
                Read(token.Colors?.Values ?? Enumerable.Empty<string>());
            }

            Read(pack.TokenImageVariants.Values.SelectMany(v => v));
            Read(pack.Combatants.Where(c => SeesFamily(c.Family)).Select(c => c.Miniature));
            Read(pack.Manifest.Figures.Where(f => SeesFigure(f.Key)).Select(f => f.Value.Image));
            Read(pack.Manifest.Portraits.Where(p => SeesFigure(p.Key)).Select(p => p.Value));
        }

        return readable;
    }
}

/// <summary>The <see cref="CardSight"/> of the phones, rebuilt after each change of a game or of a room.</summary>
public sealed class PhoneCards(RoomService rooms, IGameStore games, IContentStore content, CatalogProvider catalogs)
{
    private static readonly TimeSpan Kept = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _shown = new();
    private long _changes;
    private Computed? _last;

    private sealed record Computed(long Changes, long At, CardSight Sight);

    public void Changed() => Interlocked.Increment(ref _changes);

    public void Shown(string gameId, string picture)
    {
        _shown.GetOrAdd(gameId, _ => new())[picture] = 0;
        Changed();
    }

    /// <summary>Null: the GM's device or second screen, which reads everything.</summary>
    public async Task<CardSight?> ForAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (Network.IsLocal(context) || await rooms.ScreenGameAsync(Network.ScreenToken(context)) is not null)
        {
            return null;
        }

        // Read before computing: a change meanwhile makes the result stale at once
        var changes = Interlocked.Read(ref _changes);
        if (_last is { } last && last.Changes == changes && TimeProvider.System.GetElapsedTime(last.At) < Kept)
        {
            return last.Sight;
        }

        var sight = new CardSight(await catalogs.GetAsync(cancellationToken));
        foreach (var room in await rooms.OpenRoomsAsync())
        {
            if (await games.GetAsync(room.GameId, cancellationToken) is not { } game)
            {
                continue;
            }

            await AddAsync(sight, game, cancellationToken);
            foreach (var picture in _shown.TryGetValue(room.GameId, out var shown) ? shown.Keys : [])
            {
                sight.Show(picture);
            }
        }

        _last = new Computed(changes, TimeProvider.System.GetTimestamp(), sight);
        return sight;
    }

    public async Task<string> FingerprintAsync(Game game, CancellationToken cancellationToken)
    {
        var sight = new CardSight(await catalogs.GetAsync(cancellationToken));
        await AddAsync(sight, game, cancellationToken);
        return sight.Fingerprint();
    }

    private async Task AddAsync(CardSight sight, Game game, CancellationToken cancellationToken)
    {
        var scenario = await content.GetScenarioAsync(game.Scenario, cancellationToken);
        var played = game.State.Bounty is { } bounty ? await content.GetScenarioAsync(bounty.Scenario, cancellationToken) : scenario;
        var catalog = await catalogs.GetAsync(cancellationToken);
        var party = game.State.Campaign.Party.Select(a => a.Name).ToList();
        sight.Add(game.State, PlayerViewProjector.Project(game.Id, 0, game.State, null, party, catalog: catalog, scenario: scenario, played: played));
    }
}
