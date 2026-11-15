namespace Taleweaver.Api.Content;

/// <summary>Parts of a content id "pack:kind/name" (e.g. "um1:combatant/cave-sickle-1-3-common").</summary>
public readonly record struct ContentId(string? Pack, string? Kind, string Name)
{
    public static ContentId Parse(string id)
    {
        var colon = id.IndexOf(':', StringComparison.Ordinal);
        var pack = colon > 0 ? id[..colon] : null;
        var rest = id[(colon + 1)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? new ContentId(pack, rest[..slash], rest[(slash + 1)..]) : new ContentId(pack, null, rest);
    }
}
