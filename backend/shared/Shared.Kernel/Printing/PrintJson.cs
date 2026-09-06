using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Kernel.Printing;

/// <summary>
/// One serialiser configuration for everything that reads or writes a
/// template's jsonb columns — the DbContext's converters and the API's payloads
/// alike. Two configurations would eventually disagree about a single enum and
/// store a document neither side could read back.
/// </summary>
public static class PrintJson
{
    /// <summary>
    /// Enums are written as names, not numbers. The numeric values of
    /// <see cref="PrintSegment"/> encode the print order, so they are the kind
    /// of thing a future edit might renumber; a column full of names survives
    /// that, and is legible to anyone reading the table directly.
    /// </summary>
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) where T : new() =>
        JsonSerializer.Deserialize<T>(json, Options) ?? new T();
}
