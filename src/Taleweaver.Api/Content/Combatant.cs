using System.Text.Json;
using System.Text.Json.Serialization;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Content;

/// <summary>A combatant card (data model §2.2). One card per family and loot level.</summary>
public sealed record Combatant
{
    public required string Id { get; init; }

    public required string Family { get; init; }

    public required LocalizedText Name { get; init; }

    public LocalizedText? Subtitle { get; init; }

    /// <summary>"intelligent" (AI card) or "command" (allied, commanded by the players).</summary>
    public required string Kind { get; init; }

    /// <summary>mundane, common, uncommon, rare, "any", or null when unknown.</summary>
    public string? LootLevel { get; init; }

    /// <summary>Activation numbers as printed ("1-3", "4-6"…) or the adventurer it follows.</summary>
    public string? Activation { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public CombatantStats? Stats { get; init; }

    public IReadOnlyDictionary<string, int>? Skills { get; init; }

    public string? Stamina { get; init; }

    public IReadOnlyList<Passive> Passives { get; init; } = [];

    public IReadOnlyList<SymbolAbility> SymbolAbilities { get; init; } = [];

    public IReadOnlyList<Ability> Abilities { get; init; } = [];

    public IReadOnlyList<AiStep> AiSteps { get; init; } = [];

    public IReadOnlyList<string> MonsterLoot { get; init; } = [];

    public IReadOnlyList<string> InitiativeCards { get; init; } = [];

    public string? Image { get; init; }

    public string? ImageBack { get; init; }

    public string? Source { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<string> Review { get; init; } = [];
}

public sealed record CombatantStats
{
    public StatValue? Health { get; init; }

    public StatValue? Defense { get; init; }

    public StatValue? Movement { get; init; }

    public StatValue? Armor { get; init; }

    public IReadOnlyList<string> CombatDice { get; init; } = [];

    public IReadOnlyList<string> ConvictionDice { get; init; } = [];

    [JsonConverter(typeof(SingleOrArrayConverter<string>))]
    public IReadOnlyList<string>? CastingDie { get; init; }
}

/// <summary>A printed number, or "*" when it depends on something else (summoner, scenario…).</summary>
[JsonConverter(typeof(StatValueConverter))]
public readonly record struct StatValue(int? Value)
{
    public bool IsVariable => Value is null;

    public override string ToString() => Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "*";
}

public sealed class StatValueConverter : JsonConverter<StatValue>
{
    public override StatValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number ? new StatValue(reader.GetInt32()) : new StatValue(null);

    public override void Write(Utf8JsonWriter writer, StatValue value, JsonSerializerOptions options)
    {
        if (value.Value is { } number)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue("*");
        }
    }
}

public sealed record Passive(LocalizedText Name, LocalizedText Text);

public sealed record SymbolAbility(IReadOnlyList<SymbolCost> Cost, LocalizedText Effect);

public sealed record SymbolCost(string Symbol);

public sealed record Ability
{
    public int Cost { get; init; }

    public required LocalizedText Name { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required LocalizedText Text { get; init; }
}

/// <summary>One step of an AI card (data model §2.3): a condition, then instructions.</summary>
public sealed record AiStep
{
    public required AiCondition Condition { get; init; }

    public IReadOnlyList<AiInstruction> Instructions { get; init; } = [];

    /// <summary>CONTINUE DOWN: after the instructions, evaluate the next step too.</summary>
    public bool ContinueDown { get; init; }
}

/// <summary><see cref="Kind"/> "ask": the GM answers yes/no; "auto": the app evaluates it; "always".</summary>
public sealed record AiCondition(LocalizedText Text, string Kind);

public sealed record AiInstruction
{
    public required LocalizedText Text { get; init; }

    public string? Action { get; init; }

    public AiTarget? Target { get; init; }
}

public sealed record AiTarget(string? Priority);
