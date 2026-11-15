using System.Text.Json;
using System.Text.Json.Serialization;

namespace Taleweaver.Api.Common;

/// <summary>Reads either a single value or an array (e.g. "castingDie": "blue" or ["blue", "black"]).
/// Writes a single value back as a scalar.</summary>
public sealed class SingleOrArrayConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    public override IReadOnlyList<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return JsonSerializer.Deserialize<List<T>>(ref reader, options);
        }

        var single = JsonSerializer.Deserialize<T>(ref reader, options);
        return single is null ? [] : [single];
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            JsonSerializer.Serialize(writer, value[0], options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.ToList(), options);
        }
    }
}
