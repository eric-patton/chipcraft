using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Engine.Theory;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class SongTools
{
    [McpServerTool(Name = "create_song"), Description("Initialize a new empty song in the session.")]
    public static string CreateSong(
        SessionState session,
        [Description("Song title.")] string title = "Untitled",
        [Description("Tempo in BPM.")] int tempo = 120,
        [Description("Musical key, e.g. 'Am', 'C'.")] string key = "Am",
        [Description("Number of channels (MIDI supports up to 16).")] int channels = 8,
        [Description("Optional song author.")] string? author = null,
        [Description("Beats per bar for the song meter.")] int beatsPerBar = 4,
        [Description("Beat unit for the song meter (1, 2, 4, 8, 16, or 32).")] int beatUnit = 4,
        [Description("Tracker grid resolution in rows per beat.")] int rowsPerBeat = 4)
    {
        if (tempo <= 0)
            return JsonSerializer.Serialize(new { error = "Tempo must be greater than 0 BPM." });
        if (channels <= 0 || channels > 16)
            return JsonSerializer.Serialize(new { error = "Channel count must be between 1 and 16." });
        if (beatsPerBar <= 0)
            return JsonSerializer.Serialize(new { error = "Beats per bar must be greater than 0." });
        if (!IsSupportedBeatUnit(beatUnit))
            return JsonSerializer.Serialize(new { error = "Beat unit must be one of 1, 2, 4, 8, 16, or 32." });
        if (rowsPerBeat <= 0)
            return JsonSerializer.Serialize(new { error = "Rows per beat must be greater than 0." });

        try
        {
            _ = Key.Parse(key);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid key '{key}': {ex.Message}" });
        }

        var song = new Song
        {
            Title = title,
            Author = author,
            KeyName = key,
            Tempo = tempo,
            BeatsPerBar = beatsPerBar,
            BeatUnit = beatUnit,
            RowsPerBeat = rowsPerBeat
        };
        song.InitializeChannels(channels);

        string id = session.AddSong(song);
        return JsonSerializer.Serialize(new { songId = id, title, author, tempo, key, channels, beatsPerBar, beatUnit, rowsPerBeat });
    }

    [McpServerTool(Name = "get_song_state"), Description("Get a JSON summary of a song's current state including patterns, channel patches, timing, and order list.")]
    public static string GetSongState(
        SessionState session,
        [Description("Song ID. Omit to get the most recent song.")] string? songId = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = "No song found." });

        return JsonSerializer.Serialize(new
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
            masterVolume = song.MasterVolume,
            channelPatches = song.ChannelPrograms
                .OrderBy(kv => kv.Key)
                .Select(kv => new
                {
                    channel = kv.Key,
                    program = kv.Value.ProgramNumber,
                    name = kv.Value.Name,
                    category = kv.Value.Category,
                    bankMsb = kv.Value.BankMsb,
                    bankLsb = kv.Value.BankLsb
                }),
            channelMix = Enumerable.Range(0, song.ChannelCount).Select(channel => new
            {
                channel,
                volume = song.ChannelVolumes[channel],
                pan = song.ChannelPans[channel],
                reverbSend = song.ChannelReverbSends[channel],
                chorusSend = song.ChannelChorusSends[channel],
                muted = song.ChannelMutes[channel],
                soloed = song.ChannelSolos[channel]
            }),
            drumChannels = song.DrumChannels.OrderBy(channel => channel),
            patternCount = song.Patterns.Count,
            patterns = song.Patterns.Select((pattern, index) => new
            {
                index,
                patternId = pattern.Id,
                name = pattern.Name,
                rows = pattern.RowCount,
                channels = pattern.ChannelCount,
                partCount = pattern.Parts.Count,
                beatLength = Math.Round(pattern.RowCount / (double)Math.Max(1, song.RowsPerBeat), 3)
            }),
            orderEntries = song.OrderList.Entries.Select((entry, index) =>
            {
                var pattern = entry.PatternIndex >= 0 && entry.PatternIndex < song.Patterns.Count
                    ? song.Patterns[entry.PatternIndex]
                    : null;
                return new
                {
                    orderIndex = index,
                    patternIndex = entry.PatternIndex,
                    patternId = pattern?.Id,
                    patternName = pattern?.Name,
                    rows = pattern?.RowCount,
                    partCount = pattern?.Parts.Count,
                    tempoOverride = entry.TempoOverride,
                    effectiveTempo = entry.TempoOverride ?? song.Tempo
                };
            }),
            orderList = song.OrderList.Entries.Select(entry => entry.PatternIndex),
            loopPoint = song.OrderList.LoopStartIndex,
            totalRows = song.TotalRows,
            durationSeconds = Math.Round(song.TotalDurationSeconds, 3)
        });
    }

    [McpServerTool(Name = "set_song_metadata"), Description("Update song-level descriptive metadata such as title, author, key, and master volume.")]
    public static string SetSongMetadata(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("New title.")] string? title = null,
        [Description("New author.")] string? author = null,
        [Description("New musical key, e.g. 'Am' or 'C'.")] string? key = null,
        [Description("Optional master volume 0.0-1.0.")] float? masterVolume = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                _ = Key.Parse(key);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid key '{key}': {ex.Message}" });
            }

            song.KeyName = key;
        }

        if (title != null)
            song.Title = title;
        if (author != null)
            song.Author = author;
        if (masterVolume.HasValue)
            song.MasterVolume = Math.Clamp(masterVolume.Value, 0f, 1f);

        return JsonSerializer.Serialize(new
        {
            songId,
            title = song.Title,
            author = song.Author,
            key = song.KeyName,
            masterVolume = song.MasterVolume
        });
    }

    [McpServerTool(Name = "set_song_timing"), Description("Update song timing and transport metadata such as tempo, meter, grid resolution, and loop point.")]
    public static string SetSongTiming(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("New tempo in BPM.")] int? tempo = null,
        [Description("Beats per bar for the meter.")] int? beatsPerBar = null,
        [Description("Beat unit for the meter (1, 2, 4, 8, 16, or 32).")] int? beatUnit = null,
        [Description("Tracker grid resolution in rows per beat.")] int? rowsPerBeat = null,
        [Description("Loop start order index. Use -1 to clear looping.")] int? loopPoint = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        if (tempo.HasValue && tempo.Value <= 0)
            return JsonSerializer.Serialize(new { error = "Tempo must be greater than 0 BPM." });
        if (beatsPerBar.HasValue && beatsPerBar.Value <= 0)
            return JsonSerializer.Serialize(new { error = "Beats per bar must be greater than 0." });
        if (beatUnit.HasValue && !IsSupportedBeatUnit(beatUnit.Value))
            return JsonSerializer.Serialize(new { error = "Beat unit must be one of 1, 2, 4, 8, 16, or 32." });
        if (rowsPerBeat.HasValue && rowsPerBeat.Value <= 0)
            return JsonSerializer.Serialize(new { error = "Rows per beat must be greater than 0." });
        if (loopPoint.HasValue)
        {
            if (loopPoint.Value < -1)
                return JsonSerializer.Serialize(new { error = "Loop point must be -1 or a valid order index." });
            if (loopPoint.Value >= 0 && (song.OrderList.Entries.Count == 0 || loopPoint.Value >= song.OrderList.Entries.Count))
                return JsonSerializer.Serialize(new { error = $"Loop point {loopPoint.Value} is out of range for {song.OrderList.Entries.Count} order entries." });
        }

        if (tempo.HasValue)
            song.Tempo = tempo.Value;
        if (beatsPerBar.HasValue)
            song.BeatsPerBar = beatsPerBar.Value;
        if (beatUnit.HasValue)
            song.BeatUnit = beatUnit.Value;
        if (rowsPerBeat.HasValue)
            song.RowsPerBeat = rowsPerBeat.Value;
        if (loopPoint.HasValue)
            song.OrderList.LoopStartIndex = loopPoint.Value >= 0 ? loopPoint.Value : null;

        return JsonSerializer.Serialize(new
        {
            songId,
            tempo = song.Tempo,
            beatsPerBar = song.BeatsPerBar,
            beatUnit = song.BeatUnit,
            rowsPerBeat = song.RowsPerBeat,
            loopPoint = song.OrderList.LoopStartIndex
        });
    }

    [McpServerTool(Name = "set_order_entry_tempo"), Description("Set or clear a tempo override for one playback-order entry. Omit tempoOverride to clear it.")]
    public static string SetOrderEntryTempo(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Order index to update (0-based).")] int orderIndex,
        [Description("Tempo override in BPM. Omit to clear the override.")] int? tempoOverride = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (orderIndex < 0 || orderIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });
        if (tempoOverride.HasValue && tempoOverride.Value <= 0)
            return JsonSerializer.Serialize(new { error = "Tempo override must be greater than 0 BPM." });

        var entry = song.OrderList.Entries[orderIndex];
        song.OrderList.Entries[orderIndex] = entry with { TempoOverride = tempoOverride };

        return JsonSerializer.Serialize(new
        {
            songId,
            orderIndex,
            tempoOverride,
            effectiveTempo = tempoOverride ?? song.Tempo
        });
    }

    [McpServerTool(Name = "set_channel_mix"), Description("Adjust per-channel mix state including volume, pan, sends, mute, and solo without changing notes or patch bindings.")]
    public static string SetChannelMix(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Channel index (0-based).")] int channel,
        [Description("Optional volume 0.0-1.0.")] float? volume = null,
        [Description("Optional pan -1.0 (left) to 1.0 (right).")] float? pan = null,
        [Description("Optional reverb send 0-127.")] int? reverbSend = null,
        [Description("Optional chorus send 0-127.")] int? chorusSend = null,
        [Description("Optional mute flag.")] bool? muted = null,
        [Description("Optional solo flag.")] bool? soloed = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!IsValidChannel(song, channel))
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{song.ChannelCount - 1})." });

        if (volume.HasValue)
            song.ChannelVolumes[channel] = Math.Clamp(volume.Value, 0f, 1f);
        if (pan.HasValue)
            song.ChannelPans[channel] = Math.Clamp(pan.Value, -1f, 1f);
        if (reverbSend.HasValue)
            song.ChannelReverbSends[channel] = (byte)Math.Clamp(reverbSend.Value, 0, 127);
        if (chorusSend.HasValue)
            song.ChannelChorusSends[channel] = (byte)Math.Clamp(chorusSend.Value, 0, 127);
        if (muted.HasValue)
            song.ChannelMutes[channel] = muted.Value;
        if (soloed.HasValue)
            song.ChannelSolos[channel] = soloed.Value;

        return JsonSerializer.Serialize(new
        {
            songId,
            channel,
            volume = song.ChannelVolumes[channel],
            pan = song.ChannelPans[channel],
            reverbSend = song.ChannelReverbSends[channel],
            chorusSend = song.ChannelChorusSends[channel],
            muted = song.ChannelMutes[channel],
            soloed = song.ChannelSolos[channel]
        });
    }

    [McpServerTool(Name = "insert_pattern_to_song"), Description("Insert a pattern into a song's playback order at a specific position.")]
    public static string InsertPatternToSong(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Pattern ID to insert.")] string patternId,
        [Description("Order index to insert at (0-based). Use the current order count to append.")] int orderIndex,
        [Description("Number of times to repeat.")] int repeat = 1)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        repeat = Math.Max(1, repeat);
        if (orderIndex < 0 || orderIndex > song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count})." });

        int patternIndex = GetOrAddPatternIndex(song, pattern);
        for (int i = 0; i < repeat; i++)
            song.OrderList.Entries.Insert(orderIndex + i, new OrderEntry(patternIndex));

        if (song.OrderList.LoopStartIndex.HasValue && orderIndex <= song.OrderList.LoopStartIndex.Value)
            song.OrderList.LoopStartIndex += repeat;

        return JsonSerializer.Serialize(new
        {
            songId,
            patternId,
            patternIndex,
            orderIndex,
            repeat,
            totalOrders = song.OrderList.Entries.Count,
            loopPoint = song.OrderList.LoopStartIndex
        });
    }

    [McpServerTool(Name = "remove_order_entry"), Description("Remove one entry from a song's playback order without deleting the underlying pattern.")]
    public static string RemoveOrderEntry(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Order index to remove (0-based).")] int orderIndex)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (orderIndex < 0 || orderIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });

        song.OrderList.Entries.RemoveAt(orderIndex);
        song.OrderList.LoopStartIndex = ResolveLoopPointAfterRemoval(song.OrderList.LoopStartIndex, orderIndex, song.OrderList.Entries.Count);

        return JsonSerializer.Serialize(new
        {
            songId,
            orderIndex,
            totalOrders = song.OrderList.Entries.Count,
            loopPoint = song.OrderList.LoopStartIndex
        });
    }

    [McpServerTool(Name = "move_order_entry"), Description("Move one playback-order entry to a new position.")]
    public static string MoveOrderEntry(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Current order index (0-based).")] int fromIndex,
        [Description("New order index in the resulting order list (0-based).")] int toIndex)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (fromIndex < 0 || fromIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"From index {fromIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });

        int maxTarget = song.OrderList.Entries.Count - 1;
        if (toIndex < 0 || toIndex > maxTarget)
            return JsonSerializer.Serialize(new { error = $"To index {toIndex} out of range (0-{maxTarget})." });

        if (fromIndex != toIndex)
        {
            var entry = song.OrderList.Entries[fromIndex];
            song.OrderList.Entries.RemoveAt(fromIndex);
            song.OrderList.Entries.Insert(toIndex, entry);
            song.OrderList.LoopStartIndex = ResolveLoopPointAfterMove(song.OrderList.LoopStartIndex, fromIndex, toIndex);
        }

        return JsonSerializer.Serialize(new
        {
            songId,
            fromIndex,
            toIndex,
            totalOrders = song.OrderList.Entries.Count,
            loopPoint = song.OrderList.LoopStartIndex
        });
    }

    [McpServerTool(Name = "replace_order_entry_pattern"), Description("Replace the pattern used by a specific playback-order entry.")]
    public static string ReplaceOrderEntryPattern(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Order index to update (0-based).")] int orderIndex,
        [Description("Pattern ID to use for this order slot.")] string patternId)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (orderIndex < 0 || orderIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });

        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        int patternIndex = GetOrAddPatternIndex(song, pattern);
        var entry = song.OrderList.Entries[orderIndex];
        song.OrderList.Entries[orderIndex] = entry with { PatternIndex = patternIndex };

        return JsonSerializer.Serialize(new
        {
            songId,
            orderIndex,
            patternId,
            patternIndex,
            totalOrders = song.OrderList.Entries.Count,
            loopPoint = song.OrderList.LoopStartIndex
        });
    }

    private static bool IsSupportedBeatUnit(int beatUnit) =>
        beatUnit is 1 or 2 or 4 or 8 or 16 or 32;

    private static bool IsValidChannel(Song song, int channel) =>
        channel >= 0 && channel < song.ChannelCount;

    private static int GetOrAddPatternIndex(Song song, Pattern pattern)
    {
        int index = song.Patterns.IndexOf(pattern);
        if (index >= 0)
            return index;

        song.Patterns.Add(pattern);
        return song.Patterns.Count - 1;
    }

    private static int? ResolveLoopPointAfterRemoval(int? loopPoint, int removedIndex, int remainingCount)
    {
        if (!loopPoint.HasValue)
            return null;
        if (remainingCount == 0)
            return null;

        if (removedIndex < loopPoint.Value)
            return loopPoint.Value - 1;
        if (removedIndex == loopPoint.Value)
            return Math.Min(loopPoint.Value, remainingCount - 1);

        return Math.Min(loopPoint.Value, remainingCount - 1);
    }

    private static int? ResolveLoopPointAfterMove(int? loopPoint, int fromIndex, int toIndex)
    {
        if (!loopPoint.HasValue)
            return null;
        if (fromIndex == toIndex)
            return loopPoint;
        if (fromIndex == loopPoint.Value)
            return toIndex;

        int updatedLoopPoint = loopPoint.Value;
        if (fromIndex < updatedLoopPoint)
            updatedLoopPoint--;
        if (toIndex <= updatedLoopPoint)
            updatedLoopPoint++;

        return updatedLoopPoint;
    }
}
