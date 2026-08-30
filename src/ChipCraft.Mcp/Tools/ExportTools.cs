using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Persistence;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class ExportTools
{
    [McpServerTool(Name = "export_midi"), Description("Export a song to a Standard MIDI file (.mid). The MIDI file can be opened in any DAW or MIDI player.")]
    public static string ExportMidi(
        SessionState session,
        [Description("Song ID. Omit to export the most recent song.")] string? songId = null,
        [Description("Output file path (e.g. './output.mid').")] string outputPath = "./output.mid")
    {
        var song = songId != null ? session.GetSong(songId) : session.GetMostRecentSong();
        if (song == null)
            return JsonSerializer.Serialize(new { error = songId != null ? $"Song '{songId}' not found." : "No songs in session." });

        new MidiExporter().Export(song, outputPath, session.GetSongMetadata(song.Id));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            songId = song.Id,
            title = song.Title,
            outputPath,
            format = "MIDI",
            durationSeconds = Math.Round(song.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            channelCount = song.ChannelCount
        });
    }

    [McpServerTool(Name = "export_pattern_midi"), Description("Export a single pattern as a standalone MIDI preview. Optionally borrow timing, patches, and mix from an existing song.")]
    public static string ExportPatternMidi(
        SessionState session,
        [Description("Pattern ID to export.")] string patternId,
        [Description("Output MIDI file path.")] string outputPath = "./pattern-preview.mid",
        [Description("Optional song ID whose timing, patches, drum channels, and mix should be used for the preview.")] string? songId = null,
        [Description("Tempo in BPM used when songId is omitted.")] int tempo = 120,
        [Description("Optional title for the preview song.")] string? title = null)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var sourceSong = songId != null ? session.GetSong(songId) : FindFirstSongUsingPattern(session, pattern);
        if (songId != null && sourceSong == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        var previewSong = CreatePatternPreviewSong(pattern, sourceSong, title, tempo);
        new MidiExporter().Export(previewSong, outputPath);

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            patternId,
            songId = sourceSong?.Id,
            title = previewSong.Title,
            outputPath,
            format = "MIDI",
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            channelCount = previewSong.ChannelCount
        });
    }

    [McpServerTool(Name = "render_audio"), Description("Render a song to a WAV audio file using a SoundFont (.sf2) for high-quality instrument sounds. Requires a soundfont file path.")]
    public static string RenderAudio(
        SessionState session,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Song ID. Omit to render the most recent song.")] string? songId = null,
        [Description("Output WAV file path.")] string outputPath = "./output.wav",
        [Description("Sample rate in Hz.")] int sampleRate = 44100)
    {
        var song = songId != null ? session.GetSong(songId) : session.GetMostRecentSong();
        if (song == null)
            return JsonSerializer.Serialize(new { error = songId != null ? $"Song '{songId}' not found." : "No songs in session." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        new AudioRenderer(soundFontPath, sampleRate).RenderToWav(song, outputPath, session.GetSongMetadata(song.Id));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            songId = song.Id,
            title = song.Title,
            outputPath,
            format = "WAV",
            soundFont = Path.GetFileName(soundFontPath),
            durationSeconds = Math.Round(song.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            sampleRate
        });
    }

    [McpServerTool(Name = "render_pattern_audio"), Description("Render a single pattern as a standalone WAV preview. Optionally borrow timing, patches, and mix from an existing song.")]
    public static string RenderPatternAudio(
        SessionState session,
        [Description("Pattern ID to render.")] string patternId,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output WAV file path.")] string outputPath = "./pattern-preview.wav",
        [Description("Optional song ID whose timing, patches, drum channels, and mix should be used for the preview.")] string? songId = null,
        [Description("Tempo in BPM used when songId is omitted.")] int tempo = 120,
        [Description("Optional title for the preview song.")] string? title = null,
        [Description("Sample rate in Hz.")] int sampleRate = 44100)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        var sourceSong = songId != null ? session.GetSong(songId) : FindFirstSongUsingPattern(session, pattern);
        if (songId != null && sourceSong == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        var previewSong = CreatePatternPreviewSong(pattern, sourceSong, title, tempo);
        new AudioRenderer(soundFontPath, sampleRate).RenderToWav(previewSong, outputPath);

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            patternId,
            songId = sourceSong?.Id,
            title = previewSong.Title,
            outputPath,
            format = "WAV",
            soundFont = Path.GetFileName(soundFontPath),
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            sampleRate
        });
    }

    [McpServerTool(Name = "export_order_entry_midi"), Description("Export one playback-order entry as a standalone MIDI preview using the song's current patches, mix, and timing overrides.")]
    public static string ExportOrderEntryMidi(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Order index to preview (0-based).")] int orderIndex,
        [Description("Output MIDI file path.")] string outputPath = "./order-entry-preview.mid")
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (orderIndex < 0 || orderIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });

        var previewSong = CreateOrderEntryPreviewSong(song, orderIndex);
        new MidiExporter().Export(previewSong, outputPath, session.GetSongMetadata(songId));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            songId,
            orderIndex,
            title = previewSong.Title,
            outputPath,
            format = "MIDI",
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            channelCount = previewSong.ChannelCount
        });
    }

    [McpServerTool(Name = "render_order_entry_audio"), Description("Render one playback-order entry as a standalone WAV preview using the song's current patches, mix, and timing overrides.")]
    public static string RenderOrderEntryAudio(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Order index to preview (0-based).")] int orderIndex,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output WAV file path.")] string outputPath = "./order-entry-preview.wav",
        [Description("Sample rate in Hz.")] int sampleRate = 44100)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (orderIndex < 0 || orderIndex >= song.OrderList.Entries.Count)
            return JsonSerializer.Serialize(new { error = $"Order index {orderIndex} out of range (0-{song.OrderList.Entries.Count - 1})." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        var previewSong = CreateOrderEntryPreviewSong(song, orderIndex);
        new AudioRenderer(soundFontPath, sampleRate).RenderToWav(previewSong, outputPath, session.GetSongMetadata(songId));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            songId,
            orderIndex,
            title = previewSong.Title,
            outputPath,
            format = "WAV",
            soundFont = Path.GetFileName(soundFontPath),
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            sampleRate
        });
    }

    [McpServerTool(Name = "render_part_audio"), Description("Render one expressive part as a standalone WAV preview using the current song context when available.")]
    public static string RenderPartAudio(
        SessionState session,
        [Description("Part ID to render.")] string partId,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output WAV file path.")] string outputPath = "./part-preview.wav",
        [Description("Optional song ID whose timing, patches, drum channels, and mix should be used for the preview.")] string? songId = null,
        [Description("Sample rate in Hz.")] int sampleRate = 44100)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        var sourceSong = songId != null ? session.GetSong(songId) : FindFirstSongUsingPattern(session, pattern);
        if (songId != null && sourceSong == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        var previewSong = CreatePartPreviewSong(pattern, part, sourceSong);
        new AudioRenderer(soundFontPath, sampleRate).RenderToWav(previewSong, outputPath, session.GetSongMetadata(sourceSong?.Id));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            partId,
            patternId = pattern.Id,
            songId = sourceSong?.Id,
            title = previewSong.Title,
            outputPath,
            format = "WAV",
            soundFont = Path.GetFileName(soundFontPath),
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            sampleRate
        });
    }

    private static Song CreatePatternPreviewSong(Pattern pattern, Song? sourceSong, string? title, int tempo)
    {
        int channelCount = sourceSong != null
            ? Math.Max(sourceSong.ChannelCount, pattern.ChannelCount)
            : pattern.ChannelCount;

        var previewSong = new Song
        {
            Title = title ?? $"{pattern.Name} Preview",
            Author = sourceSong?.Author,
            KeyName = sourceSong?.KeyName,
            Tempo = sourceSong?.Tempo ?? tempo,
            RowsPerBeat = sourceSong?.RowsPerBeat ?? 4,
            BeatsPerBar = sourceSong?.BeatsPerBar ?? 4,
            BeatUnit = sourceSong?.BeatUnit ?? 4,
            MasterVolume = sourceSong?.MasterVolume ?? 0.8f
        };
        previewSong.InitializeChannels(channelCount);
        ApplySongContext(previewSong, sourceSong);
        EnsureDefaultPrograms(previewSong);
        previewSong.Patterns.Add(ClonePattern(pattern));
        previewSong.OrderList.Entries.Add(new OrderEntry(0));
        return previewSong;
    }

    private static Song CreateOrderEntryPreviewSong(Song sourceSong, int orderIndex)
    {
        var entry = sourceSong.OrderList.Entries[orderIndex];
        var pattern = sourceSong.Patterns[entry.PatternIndex];

        var previewSong = new Song
        {
            Title = $"{sourceSong.Title} Entry {orderIndex}",
            Author = sourceSong.Author,
            KeyName = sourceSong.KeyName,
            Tempo = sourceSong.Tempo,
            RowsPerBeat = sourceSong.RowsPerBeat,
            BeatsPerBar = sourceSong.BeatsPerBar,
            BeatUnit = sourceSong.BeatUnit,
            MasterVolume = sourceSong.MasterVolume
        };
        previewSong.InitializeChannels(Math.Max(sourceSong.ChannelCount, pattern.ChannelCount));
        ApplySongContext(previewSong, sourceSong);
        previewSong.Patterns.Add(ClonePattern(pattern));
        previewSong.OrderList.Entries.Add(new OrderEntry(0, entry.TempoOverride));
        return previewSong;
    }

    private static Song CreatePartPreviewSong(Pattern sourcePattern, Part sourcePart, Song? sourceSong)
    {
        var previewPattern = new Pattern(sourcePattern.RowCount, sourcePattern.ChannelCount) { Name = $"{sourcePart.Name} Part" };
        previewPattern.Parts.Add(ClonePart(sourcePart));

        var previewSong = CreatePatternPreviewSong(previewPattern, sourceSong, $"{sourcePart.Name} Preview", sourceSong?.Tempo ?? 120);
        if (sourcePart.ProgramOverride != null)
            previewSong.ChannelPrograms[sourcePart.Channel] = sourcePart.ProgramOverride;
        return previewSong;
    }

    [McpServerTool(Name = "render_stems"), Description("Render one WAV stem and one MIDI stem per audible channel in the song.")]
    public static string RenderStems(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output directory for rendered stems.")] string outputDir)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        string resolvedDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(resolvedDir);
        string slug = Slugify(song.Title);
        var stems = new List<object>();
        var renderer = new AudioRenderer(soundFontPath);
        var exporter = new MidiExporter();

        foreach (int channel in Enumerable.Range(0, song.ChannelCount).Where(ch => ChannelHasContent(song, ch)))
        {
            var stemSong = CreateChannelStemSong(song, channel);
            string stemName = $"ch{channel:D2}-{Slugify(song.ChannelPrograms.GetValueOrDefault(channel)?.Name ?? (song.DrumChannels.Contains(channel) ? "drums" : "part"))}";
            string midiPath = Path.Combine(resolvedDir, $"{slug}.{stemName}.mid");
            string wavPath = Path.Combine(resolvedDir, $"{slug}.{stemName}.wav");

            exporter.Export(stemSong, midiPath, session.GetSongMetadata(songId));
            renderer.RenderToWav(stemSong, wavPath, session.GetSongMetadata(songId));

            stems.Add(new
            {
                channel,
                name = stemName,
                midiPath,
                wavPath
            });
        }

        return JsonSerializer.Serialize(new { songId, outputDir = resolvedDir, count = stems.Count, stems });
    }

    [McpServerTool(Name = "render_loop_preview"), Description("Render the loop region plus a short post-seam slice so the loop transition can be auditioned offline.")]
    public static string RenderLoopPreview(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output WAV file path.")] string outputPath,
        [Description("How many bars from the loop start to append after the loop seam.")] int seamBars = 1,
        [Description("Sample rate in Hz.")] int sampleRate = 44100)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });
        if (!song.OrderList.LoopStartIndex.HasValue)
            return JsonSerializer.Serialize(new { error = "Song does not have a loop point." });
        if (seamBars <= 0)
            return JsonSerializer.Serialize(new { error = "Seam bars must be greater than 0." });

        var previewSong = CreateLoopPreviewSong(song, seamBars);
        new AudioRenderer(soundFontPath, sampleRate).RenderToWav(previewSong, outputPath, session.GetSongMetadata(songId));

        var fileInfo = new FileInfo(outputPath);
        return JsonSerializer.Serialize(new
        {
            songId,
            outputPath,
            format = "WAV",
            durationSeconds = Math.Round(previewSong.TotalDurationSeconds, 3),
            fileSizeBytes = fileInfo.Length,
            seamBars,
            sampleRate
        });
    }

    [McpServerTool(Name = "export_delivery_bundle"), Description("Render a production-oriented delivery bundle containing final audio, optional loop preview, stems, MIDI, song JSON, and a manifest.")]
    public static string ExportDeliveryBundle(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Path to a .sf2 SoundFont file.")] string soundFontPath,
        [Description("Output directory for the bundle.")] string outputDir,
        [Description("Include a full-song MIDI export.")] bool includeMidi = true,
        [Description("Include the song JSON persistence file.")] bool includeSongJson = true,
        [Description("Include per-channel stems.")] bool includeStems = true,
        [Description("Include a full-mix WAV preview.")] bool includePreview = true,
        [Description("Include a loop-preview WAV when the song has a loop point.")] bool includeLoopPreview = true,
        [Description("Sample rate in Hz.")] int sampleRate = 44100,
        [Description("How many bars to include after the loop seam when includeLoopPreview is enabled.")] int seamBars = 1)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!File.Exists(soundFontPath))
            return JsonSerializer.Serialize(new { error = $"SoundFont not found: {soundFontPath}" });

        string resolvedDir = SongArtifactUtilities.ResolveOutputDirectory(song.Title, outputDir);
        Directory.CreateDirectory(resolvedDir);
        string slug = Slugify(song.Title);
        string? midiPath = null;
        string? songJsonPath = null;
        string? fullMixPath = null;
        string? loopPreviewPath = null;
        object[] stems = [];
        var exporter = new MidiExporter();
        var renderer = new AudioRenderer(soundFontPath, sampleRate);
        var metadata = session.GetSongMetadata(songId);

        if (includeMidi)
        {
            midiPath = Path.Combine(resolvedDir, $"{slug}.mid");
            exporter.Export(song, midiPath, metadata);
        }

        if (includeSongJson)
        {
            songJsonPath = Path.Combine(resolvedDir, $"{slug}.song.json");
            File.WriteAllText(songJsonPath, SongSerializer.Serialize(song), Encoding.UTF8);
        }

        if (includePreview)
        {
            fullMixPath = Path.Combine(resolvedDir, $"{slug}.mix.wav");
            renderer.RenderToWav(song, fullMixPath, metadata);
        }

        if (includeLoopPreview && song.OrderList.LoopStartIndex.HasValue)
        {
            loopPreviewPath = Path.Combine(resolvedDir, $"{slug}.loop-preview.wav");
            renderer.RenderToWav(CreateLoopPreviewSong(song, seamBars), loopPreviewPath, metadata);
        }

        if (includeStems)
        {
            string stemsDir = Path.Combine(resolvedDir, "stems");
            Directory.CreateDirectory(stemsDir);
            stems = Enumerable.Range(0, song.ChannelCount)
                .Where(channel => ChannelHasContent(song, channel))
                .Select(channel =>
                {
                    var stemSong = CreateChannelStemSong(song, channel);
                    string stemName = $"ch{channel:D2}-{Slugify(song.ChannelPrograms.GetValueOrDefault(channel)?.Name ?? (song.DrumChannels.Contains(channel) ? "drums" : "part"))}";
                    string stemMidiPath = Path.Combine(stemsDir, $"{slug}.{stemName}.mid");
                    string stemWavPath = Path.Combine(stemsDir, $"{slug}.{stemName}.wav");
                    exporter.Export(stemSong, stemMidiPath, metadata);
                    renderer.RenderToWav(stemSong, stemWavPath, metadata);
                    return new
                    {
                        channel,
                        name = stemName,
                        midiPath = stemMidiPath,
                        wavPath = stemWavPath
                    };
                })
                .Cast<object>()
                .ToArray();
        }

        string manifestPath = Path.Combine(resolvedDir, "delivery-manifest.json");
        var manifest = new
        {
            songId = song.Id,
            title = song.Title,
            author = song.Author,
            key = song.KeyName,
            tempo = song.Tempo,
            beatsPerBar = song.BeatsPerBar,
            beatUnit = song.BeatUnit,
            rowsPerBeat = song.RowsPerBeat,
            renderSettings = new
            {
                soundFontPath = Path.GetFullPath(soundFontPath),
                sampleRate,
                seamBars
            },
            channelPatches = song.ChannelPrograms.OrderBy(kv => kv.Key).Select(kv => new
            {
                channel = kv.Key,
                program = kv.Value.ProgramNumber,
                name = kv.Value.Name,
                category = kv.Value.Category,
                bankMsb = kv.Value.BankMsb,
                bankLsb = kv.Value.BankLsb
            }),
            analysis = metadata?.Analysis,
            artifacts = new
            {
                outputDir = resolvedDir,
                midiPath,
                songJsonPath,
                fullMixPath,
                loopPreviewPath,
                stems
            },
            createdAtUtc = DateTime.UtcNow
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, McpToolJson.SerializerOptions), Encoding.UTF8);

        return JsonSerializer.Serialize(new
        {
            songId,
            outputDir = resolvedDir,
            manifestPath,
            midiPath,
            songJsonPath,
            fullMixPath,
            loopPreviewPath,
            stemCount = stems.Length
        });
    }

    private static Song CreateLoopPreviewSong(Song sourceSong, int seamBars)
    {
        if (!sourceSong.OrderList.LoopStartIndex.HasValue)
            throw new InvalidOperationException("Song does not define a loop point.");

        var previewSong = new Song
        {
            Title = $"{sourceSong.Title} Loop Preview",
            Author = sourceSong.Author,
            KeyName = sourceSong.KeyName,
            Tempo = sourceSong.Tempo,
            RowsPerBeat = sourceSong.RowsPerBeat,
            BeatsPerBar = sourceSong.BeatsPerBar,
            BeatUnit = sourceSong.BeatUnit,
            MasterVolume = sourceSong.MasterVolume
        };
        previewSong.InitializeChannels(sourceSong.ChannelCount);
        ApplySongContext(previewSong, sourceSong);

        int loopPoint = sourceSong.OrderList.LoopStartIndex.Value;
        for (int index = loopPoint; index < sourceSong.OrderList.Entries.Count; index++)
        {
            var sourceEntry = sourceSong.OrderList.Entries[index];
            var clonedPattern = ClonePattern(sourceSong.Patterns[sourceEntry.PatternIndex]);
            previewSong.Patterns.Add(clonedPattern);
            previewSong.OrderList.Entries.Add(new OrderEntry(previewSong.Patterns.Count - 1, sourceEntry.TempoOverride));
        }

        int seamRowsRemaining = seamBars * sourceSong.BeatsPerBar * sourceSong.RowsPerBeat;
        int scanIndex = loopPoint;
        while (seamRowsRemaining > 0 && scanIndex < sourceSong.OrderList.Entries.Count)
        {
            var sourceEntry = sourceSong.OrderList.Entries[scanIndex];
            var sourcePattern = sourceSong.Patterns[sourceEntry.PatternIndex];
            int rowsToTake = Math.Min(seamRowsRemaining, sourcePattern.RowCount);
            var slice = SlicePattern(sourcePattern, 0, rowsToTake, sourceSong.RowsPerBeat);
            previewSong.Patterns.Add(slice);
            previewSong.OrderList.Entries.Add(new OrderEntry(previewSong.Patterns.Count - 1, sourceEntry.TempoOverride));
            seamRowsRemaining -= rowsToTake;
            scanIndex++;
        }

        return previewSong;
    }

    private static void ApplySongContext(Song targetSong, Song? sourceSong)
    {
        if (sourceSong == null)
            return;

        int sharedChannels = Math.Min(targetSong.ChannelCount, sourceSong.ChannelCount);
        for (int channel = 0; channel < sharedChannels; channel++)
        {
            targetSong.ChannelVolumes[channel] = sourceSong.ChannelVolumes[channel];
            targetSong.ChannelPans[channel] = sourceSong.ChannelPans[channel];
            targetSong.ChannelReverbSends[channel] = sourceSong.ChannelReverbSends[channel];
            targetSong.ChannelChorusSends[channel] = sourceSong.ChannelChorusSends[channel];
            targetSong.ChannelMutes[channel] = sourceSong.ChannelMutes[channel];
            targetSong.ChannelSolos[channel] = sourceSong.ChannelSolos[channel];
        }

        foreach (var patch in sourceSong.ChannelPrograms.Where(kv => kv.Key < targetSong.ChannelCount))
            targetSong.ChannelPrograms[patch.Key] = patch.Value;
        foreach (int drumChannel in sourceSong.DrumChannels.Where(channel => channel < targetSong.ChannelCount))
            targetSong.DrumChannels.Add(drumChannel);
    }

    private static void EnsureDefaultPrograms(Song song)
    {
        var piano = GeneralMidi.GetProgram(0);
        for (int channel = 0; channel < song.ChannelCount; channel++)
        {
            if (song.DrumChannels.Contains(channel))
            {
                song.ChannelPrograms[channel] = MidiProgram.Drums;
                continue;
            }

            if (!song.ChannelPrograms.ContainsKey(channel))
                song.ChannelPrograms[channel] = piano;
        }
    }

    private static Pattern ClonePattern(Pattern pattern) =>
        SongSerializer.Deserialize(SongSerializer.Serialize(new Song
        {
            Title = "Pattern Clone Carrier",
            Tempo = 120,
            RowsPerBeat = 4,
            BeatsPerBar = 4,
            BeatUnit = 4,
            ChannelCount = pattern.ChannelCount,
            ChannelVolumes = new float[pattern.ChannelCount],
            ChannelPans = new float[pattern.ChannelCount],
            ChannelReverbSends = new byte[pattern.ChannelCount],
            ChannelChorusSends = new byte[pattern.ChannelCount],
            ChannelMutes = new bool[pattern.ChannelCount],
            ChannelSolos = new bool[pattern.ChannelCount],
            Patterns = [pattern],
            OrderList = new OrderList { Entries = [new OrderEntry(0)] }
        })).Patterns[0];

    private static Part ClonePart(Part part) =>
        SongSerializer.Deserialize(SongSerializer.Serialize(new Song
        {
            Title = "Part Clone Carrier",
            Tempo = 120,
            RowsPerBeat = 4,
            BeatsPerBar = 4,
            BeatUnit = 4,
            ChannelCount = Math.Max(1, part.Channel + 1),
            ChannelVolumes = new float[Math.Max(1, part.Channel + 1)],
            ChannelPans = new float[Math.Max(1, part.Channel + 1)],
            ChannelReverbSends = new byte[Math.Max(1, part.Channel + 1)],
            ChannelChorusSends = new byte[Math.Max(1, part.Channel + 1)],
            ChannelMutes = new bool[Math.Max(1, part.Channel + 1)],
            ChannelSolos = new bool[Math.Max(1, part.Channel + 1)],
            Patterns = [new Pattern(16, Math.Max(1, part.Channel + 1)) { Parts = { part } }],
            OrderList = new OrderList { Entries = [new OrderEntry(0)] }
        })).Patterns[0].Parts[0];

    private static Pattern SlicePattern(Pattern sourcePattern, int startRow, int rowCount, int rowsPerBeat)
    {
        var slice = new Pattern(rowCount, sourcePattern.ChannelCount) { Name = $"{sourcePattern.Name} Slice" };

        for (int row = 0; row < rowCount; row++)
        {
            for (int channel = 0; channel < sourcePattern.ChannelCount; channel++)
            {
                var cell = sourcePattern.GetCell(startRow + row, channel);
                if (!cell.IsEmpty)
                    slice.SetCell(row, channel, cell);
            }
        }

        float sliceStartBeat = startRow / (float)Math.Max(1, rowsPerBeat);
        float sliceEndBeat = (startRow + rowCount) / (float)Math.Max(1, rowsPerBeat);
        foreach (var sourcePart in sourcePattern.Parts)
        {
            var part = ClonePart(sourcePart);
            part.Notes = sourcePart.Notes
                .Where(note => note.StartBeat < sliceEndBeat && note.EndBeat > sliceStartBeat)
                .Select(note =>
                {
                    float clippedStart = Math.Max(note.StartBeat, sliceStartBeat);
                    float clippedEnd = Math.Min(note.EndBeat, sliceEndBeat);
                    return note with
                    {
                        StartBeat = clippedStart - sliceStartBeat,
                        DurationBeats = clippedEnd - clippedStart
                    };
                })
                .Where(note => note.DurationBeats > 0)
                .ToList();
            part.AutomationLanes = sourcePart.AutomationLanes
                .Select(lane => new AutomationLane
                {
                    Type = lane.Type,
                    Points = lane.Points
                        .Where(point => point.Beat >= sliceStartBeat && point.Beat <= sliceEndBeat)
                        .Select(point => new AutomationPoint(point.Beat - sliceStartBeat, point.Value))
                        .ToList()
                })
                .Where(lane => lane.Points.Count > 0)
                .ToList();

            if (part.Notes.Count > 0 || part.AutomationLanes.Count > 0 || part.ProgramOverride != null)
                slice.Parts.Add(part);
        }

        return slice;
    }

    private static Song? FindFirstSongUsingPattern(SessionState session, Pattern pattern) =>
        session.ListSongs().FirstOrDefault(song => song.Patterns.Contains(pattern));

    private static bool ChannelHasContent(Song song, int channel)
    {
        foreach (var pattern in song.Patterns)
        {
            if (channel < pattern.ChannelCount)
            {
                for (int row = 0; row < pattern.RowCount; row++)
                {
                    if (!pattern.GetCell(row, channel).IsEmpty)
                        return true;
                }
            }

            if (pattern.Parts.Any(part => part.Channel == channel && (part.Notes.Count > 0 || part.AutomationLanes.Count > 0)))
                return true;
        }

        return false;
    }

    private static Song CreateChannelStemSong(Song sourceSong, int targetChannel)
    {
        var stemSong = SongSerializer.Deserialize(SongSerializer.Serialize(sourceSong));
        Array.Fill(stemSong.ChannelMutes, true);
        Array.Fill(stemSong.ChannelSolos, false);
        if (targetChannel < stemSong.ChannelMutes.Length)
            stemSong.ChannelMutes[targetChannel] = false;
        return stemSong;
    }

    private static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        string slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}
