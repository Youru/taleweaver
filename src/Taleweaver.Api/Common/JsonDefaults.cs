using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Taleweaver.Api.Common;

/// <summary>An empty list that says something ("nothing bought", "no names: prepare the party"): written in the files too.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeptWhenEmptyAttribute : Attribute;

/// <summary>Serializer settings shared by the content files, the saves and the HTTP API.</summary>
public static class JsonDefaults
{
    /// <summary>HTTP API and reading: camelCase, nulls omitted, empty lists kept (simpler for the front).</summary>
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Files written by the app: indented, and without empty lists, like the hand-written files.</summary>
    public static JsonSerializerOptions FileOptions { get; } = new(Options)
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
    };

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.AllowTrailingCommas = true;
        options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        return options;
    }

    private static void OmitEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                && !property.IsRequired && property.AttributeProvider?.IsDefined(typeof(KeptWhenEmptyAttribute), inherit: false) != true)
            {
                property.ShouldSerialize = (_, value) => value is not ICollection { Count: 0 };
            }
        }
    }
}
