using System.Text.Json;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Storage;

/// <summary>Writes a file through a temporary file and a rename, so that a crash never leaves it half written.</summary>
internal static class AtomicFile
{
    public static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.FileOptions, cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
