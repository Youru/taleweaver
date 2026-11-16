namespace Taleweaver.Api.Content;

/// <summary>
/// Which packs are enabled (data-model §2.1): those asked for (all when none is named), less those
/// whose dependencies are not enabled, dependencies first so that a pack may override their cards.
/// </summary>
public static class PackSelection
{
    /// <param name="Enabled">The enabled packs, each after the packs it depends on.</param>
    /// <param name="Missing">The packs left out, with the dependencies they lack.</param>
    public sealed record Choice(IReadOnlyList<Pack> Enabled, IReadOnlyDictionary<string, IReadOnlyList<string>> Missing);

    public static Choice Choose(IReadOnlyList<Pack> packs, IReadOnlyCollection<string> wanted)
    {
        var byId = packs.ToDictionary(p => p.Id);
        var kept = packs.Where(p => wanted.Count == 0 || wanted.Contains(p.Id)).Select(p => p.Id).ToHashSet();

        // A pack whose dependency is left out goes too (which may leave out others in turn)
        var missing = new Dictionary<string, IReadOnlyList<string>>();
        bool changed;
        do
        {
            changed = false;
            foreach (var id in kept.ToList())
            {
                var lacks = byId[id].Manifest.DependsOn.Where(d => !kept.Contains(d)).ToList();
                if (lacks.Count > 0)
                {
                    kept.Remove(id);
                    missing[id] = lacks;
                    changed = true;
                }
            }
        }
        while (changed);

        var ordered = new List<Pack>();
        var placed = new HashSet<string>();
        void Place(string id, HashSet<string> visiting)
        {
            // A cycle of dependencies is cut where it closes
            if (placed.Contains(id) || !visiting.Add(id))
            {
                return;
            }

            foreach (var dependency in byId[id].Manifest.DependsOn.Where(kept.Contains))
            {
                Place(dependency, visiting);
            }

            placed.Add(id);
            ordered.Add(byId[id]);
        }

        foreach (var pack in packs.Where(p => kept.Contains(p.Id)))
        {
            Place(pack.Id, []);
        }

        return new Choice(ordered, missing);
    }
}
