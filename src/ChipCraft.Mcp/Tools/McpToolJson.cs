using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChipCraft.Mcp.Tools;

internal static class McpToolJson
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
