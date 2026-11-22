using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The party being made before the departure (roadmap §5.2). Only <see cref="Choices"/> and <see cref="HiddenShown"/>
/// are chosen; the rest is what the scenario's rules give, kept here so that the GM and the phones read one answer.
/// </summary>
public sealed class PartyPreparation
{
    public List<PartyChoice> Choices { get; set; } = [];

    /// <summary>The GM offers the adventurers still to unlock (spoilers).</summary>
    public bool HiddenShown { get; set; }

    public int Min { get; set; }

    public int Max { get; set; }

    /// <summary>Gold of the party before buying, and what is left of it.</summary>
    public int Gold { get; set; }

    public int GoldLeft { get; set; }

    public int Xp { get; set; }

    public bool Custom { get; set; }

    public List<AdventurerOffer> Adventurers { get; set; } = [];

    public List<StarterKit> Kits { get; set; } = [];

    public bool ItemsOpen { get; set; }

    public List<ItemOffer> Items { get; set; } = [];

    public int? MaxItems { get; set; }

    /// <summary>Catalogue ids of the Disciplines that may be bought.</summary>
    public List<string> Disciplines { get; set; } = [];

    public int? MaxDisciplines { get; set; }

    /// <summary>Error code of what still prevents the departure; null: the GM may set out.</summary>
    public string? Missing { get; set; }
}

/// <summary>One adventurer's choices: a kit, and what is bought.</summary>
public sealed record PartyChoice
{
    public required string Adventurer { get; init; }

    public string? Kit { get; init; }

    /// <summary>Null: as before; empty: nothing.</summary>
    [KeptWhenEmpty]
    public IReadOnlyList<string>? Items { get; init; }

    [KeptWhenEmpty]
    public IReadOnlyList<string>? Disciplines { get; init; }
}

public sealed record AdventurerOffer(string Name, string Id, string? Image, bool Hidden, bool Required);

public sealed record ItemOffer(string Item, int Price);
