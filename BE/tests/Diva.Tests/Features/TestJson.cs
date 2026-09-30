using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diva.Tests.Features;

/// <summary>Reads API responses the way the API writes them (web defaults, enums as text).</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
