using System.Text.Json;
using System.Text.Json.Serialization;

namespace Caesarea.ServiceDefaults;

/// <summary>
/// Provides the shared JSON contract used by Caesarea HTTP APIs and their typed clients.
/// </summary>
public static class CaesareaJsonDefaults
{
    /// <summary>
    /// Creates web-compatible serializer options with string enum support.
    /// </summary>
    /// <returns>A new serializer-options instance safe for exclusive use by one client type.</returns>
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    /// <summary>
    /// Applies the Caesarea JSON contract to an existing serializer-options instance.
    /// </summary>
    /// <param name="options">The serializer options to configure.</param>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }
}
