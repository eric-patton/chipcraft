using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class SessionTools
{
    [McpServerTool(Name = "list_patterns"), Description("List patterns currently stored in the MCP session.")]
    public static string ListPatterns(SessionState session)
    {
        var patterns = session.ListPatterns()
            .OrderBy(pattern => pattern.Id, StringComparer.Ordinal)
            .Select(pattern => new
            {
                patternId = pattern.Id,
                name = pattern.Name,
                rows = pattern.RowCount,
                channels = pattern.ChannelCount,
                cellCount = CountOccupiedCells(pattern),
                partCount = pattern.Parts.Count
            })
            .ToArray();

        return JsonSerializer.Serialize(new { count = patterns.Length, patterns });
    }

    [McpServerTool(Name = "list_songs"), Description("List songs currently stored in the MCP session.")]
    public static string ListSongs(SessionState session)
    {
        var songs = session.ListSongs()
            .OrderBy(song => song.Id, StringComparer.Ordinal)
            .Select(song => new
            {
                songId = song.Id,
                title = song.Title,
                author = song.Author,
                key = song.KeyName,
                tempo = song.Tempo,
                beatsPerBar = song.BeatsPerBar,
                beatUnit = song.BeatUnit,
                rowsPerBeat = song.RowsPerBeat,
                channelCount = song.ChannelCount,
                patternCount = song.Patterns.Count,
                orderCount = song.OrderList.Entries.Count,
                loopPoint = song.OrderList.LoopStartIndex,
                durationSeconds = Math.Round(song.TotalDurationSeconds, 1)
            })
            .ToArray();

        return JsonSerializer.Serialize(new { count = songs.Length, songs });
    }

    private static int CountOccupiedCells(Pattern pattern)
    {
        int count = 0;
        for (int row = 0; row < pattern.RowCount; row++)
        {
            for (int channel = 0; channel < pattern.ChannelCount; channel++)
            {
                if (!pattern.GetCell(row, channel).IsEmpty)
                    count++;
            }
        }

        return count;
    }
}
