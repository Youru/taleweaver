using System.Runtime.CompilerServices;

namespace Taleweaver.UnitTests;

/// <summary>
/// Where the game's content is (packs/um1, scenarios/, reference/, assets/): the TALEWEAVER_CONTENT_ROOT variable, or a
/// folder above the tests. Null in the taleweaver repository alone (its CI): the content is never published.
/// </summary>
public static class ContentRoot
{
    private static readonly Lazy<string?> Found = new(() =>
    {
        if (Environment.GetEnvironmentVariable("TALEWEAVER_CONTENT_ROOT") is { Length: > 0 } variable)
        {
            return System.IO.Path.GetFullPath(variable);
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "packs", "um1", "pack.json")))
            {
                return dir.FullName;
            }
        }

        return null;
    });

    public static string? Path => Found.Value;

    public const string Missing = "The game's content is not here (TALEWEAVER_CONTENT_ROOT)";
}

/// <summary>A test on the game's content: skipped where it is absent.</summary>
public sealed class ContentFactAttribute : FactAttribute
{
    public ContentFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (ContentRoot.Path is null)
        {
            Skip = ContentRoot.Missing;
        }
    }
}

/// <summary>A theory on the game's content: skipped where it is absent.</summary>
public sealed class ContentTheoryAttribute : TheoryAttribute
{
    public ContentTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (ContentRoot.Path is null)
        {
            Skip = ContentRoot.Missing;
        }
    }
}
