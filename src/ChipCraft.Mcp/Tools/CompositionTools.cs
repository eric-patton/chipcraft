using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Engine.Theory;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class CompositionTools
{
    [McpServerTool(Name = "create_pattern"), Description("Create an empty pattern grid. Patterns are the building blocks of songs.")]
    public static string CreatePattern(
        SessionState session,
        [Description("Number of rows (time steps). Common: 16, 32, 64.")] int rows = 64,
        [Description("Number of channels.")] int channels = 4,
        [Description("Optional pattern name.")] string? name = null)
    {
        var pattern = new Pattern(rows, channels) { Name = name ?? "Pattern" };
        string id = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = id, rows, channels, name = pattern.Name });
    }

    [McpServerTool(Name = "set_notes"), Description("Place notes in a pattern. Provide an array of note placements as JSON.")]
    public static string SetNotes(
        SessionState session,
        [Description("Pattern ID to modify.")] string patternId,
        [Description("""JSON array of notes: [{"row":0,"channel":0,"note":"C4","instrument":"inst_001","volume":12}, ...]. Use "---" for rest, "===" for note-off.""")] string notes)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var noteList = JsonSerializer.Deserialize<List<NoteInput>>(notes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (noteList == null)
            return JsonSerializer.Serialize(new { error = "Invalid notes JSON." });

        int placed = 0;
        foreach (var n in noteList)
        {
            if (n.Row < 0 || n.Row >= pattern.RowCount || n.Channel < 0 || n.Channel >= pattern.ChannelCount)
                continue;

            var note = Note.Parse(n.Note);
            byte? vol = n.Volume.HasValue ? (byte)Math.Clamp(n.Volume.Value, 0, 15) : null;
            pattern.SetCell(n.Row, n.Channel, new PatternCell(note, n.Instrument, vol));
            placed++;
        }

        return JsonSerializer.Serialize(new { patternId, notesPlaced = placed });
    }

    [McpServerTool(Name = "generate_melody"), Description("Generate a melody using music theory. Returns a pattern with notes constrained by key, scale, and chord progression.")]
    public static string GenerateMelody(
        SessionState session,
        [Description("Musical key, e.g. 'Am', 'C', 'Dm'.")] string key,
        [Description("Scale: Major, NaturalMinor, PentatonicMinor, Blues, Dorian, Mixolydian, etc.")] string scaleType = "NaturalMinor",
        [Description("Contour: Ascending, Descending, Arch, Valley, Flat.")] string contour = "Arch",
        [Description("Number of bars.")] int bars = 4,
        [Description("Energy level (0.0-1.0). Higher = shorter, busier notes.")] float energy = 0.5f,
        [Description("Optional chord progression as space-separated symbols, e.g. 'Am F C G'.")] string? chords = null,
        [Description("Instrument ID for the melody notes.")] string? instrumentId = null)
    {
        var keyObj = Key.Parse(key);
        if (Enum.TryParse<ScaleType>(scaleType, true, out var st))
            keyObj = new Key(keyObj.Root, st);

        var contourEnum = Enum.Parse<MelodyContour>(contour, ignoreCase: true);
        var generator = new MelodyGenerator();
        ChordProgression? progression = null;
        if (!string.IsNullOrWhiteSpace(chords))
        {
            var chordSymbols = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chordList = chordSymbols.Select(symbol => new ChordEvent(Chord.Parse(symbol))).ToList();
            while (chordList.Count < bars)
                chordList.AddRange(chordSymbols.Select(symbol => new ChordEvent(Chord.Parse(symbol))));
            if (chordList.Count > bars)
                chordList = chordList.Take(bars).ToList();

            progression = new ChordProgression
            {
                Key = keyObj,
                Chords = chordList
            };
        }

        var melody = generator.Generate(new MelodyOptions(keyObj, contourEnum, bars, Energy: energy, Progression: progression));
        var pattern = new Pattern(bars * 16, 1) { Name = "Melody" };
        pattern.ApplyNoteSequence(melody, 0, instrumentId ?? "", 4);

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new
        {
            patternId = patId,
            bars,
            noteCount = melody.Events.Count(e => !e.IsRest),
            key = keyObj.ToString(),
            contour
        });
    }

    [McpServerTool(Name = "generate_bass_line"), Description("Generate a bass line from a chord progression.")]
    public static string GenerateBassLine(
        SessionState session,
        [Description("Chord progression as space-separated symbols, e.g. 'Am F C G'.")] string chords,
        [Description("Musical key.")] string key,
        [Description("Style: RootFifth, Octave, Walking, Pedal, Arpeggiated.")] string style = "RootFifth",
        [Description("Number of bars.")] int bars = 4,
        [Description("Instrument ID for the bass notes.")] string? instrumentId = null)
    {
        var keyObj = Key.Parse(key);
        var styleEnum = Enum.Parse<BassStyle>(style, ignoreCase: true);

        // Parse chord symbols and expand/truncate to match bar count
        var chordSymbols = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chordList = chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))).ToList();

        while (chordList.Count < bars)
            chordList.AddRange(chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))));
        if (chordList.Count > bars)
            chordList = chordList.Take(bars).ToList();

        var progression = new ChordProgression
        {
            Key = keyObj,
            Chords = chordList
        };

        var generator = new BassLineGenerator();
        var bassLine = generator.Generate(new BassLineOptions(progression, styleEnum, Octave: 2));
        var pattern = new Pattern(bars * 16, 1) { Name = "Bass" };
        pattern.ApplyNoteSequence(bassLine, 0, instrumentId ?? "", 4);

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = patId, bars, style, chords });
    }

    [McpServerTool(Name = "generate_drums"), Description("Generate a drum pattern by style and energy level.")]
    public static string GenerateDrums(
        SessionState session,
        [Description("Style: StraightRock, Shuffle, FourOnFloor, HalfTime, DoubleTime, Breakbeat, March.")] string style = "StraightRock",
        [Description("Energy 1-10. 1=sparse, 5=standard, 10=intense.")] int energy = 5,
        [Description("Number of bars.")] int bars = 4,
        [Description("Include fills at phrase boundaries.")] bool fills = true)
    {
        var styleEnum = Enum.Parse<DrumStyle>(style, ignoreCase: true);
        var generator = new DrumPatternGenerator();
        var drums = generator.Generate(new DrumPatternOptions(styleEnum, energy, bars, Fills: fills));

        var pattern = new Pattern(bars * 16, 1) { Name = "Drums" };
        pattern.ApplyDrumPattern(drums, 0, 4);

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = patId, bars, style, energy, hitCount = drums.Hits.Count });
    }

    [McpServerTool(Name = "generate_harmony"), Description("Generate a harmony part from an existing melody and chord progression. Styles: ThirdsBelow, SixthsBelow, ArpeggiatedChords, Countermelody.")]
    public static string GenerateHarmony(
        SessionState session,
        [Description("Pattern ID containing the melody to harmonize.")] string melodyPatternId,
        [Description("Chord progression as space-separated symbols, e.g. 'Am F C G'.")] string chords,
        [Description("Musical key.")] string key,
        [Description("Style: ThirdsBelow, SixthsBelow, ArpeggiatedChords, Countermelody.")] string style = "ThirdsBelow",
        [Description("Number of bars.")] int bars = 4)
    {
        var melodyPattern = session.GetPattern(melodyPatternId);
        if (melodyPattern == null)
            return JsonSerializer.Serialize(new { error = $"Melody pattern '{melodyPatternId}' not found." });

        var keyObj = Key.Parse(key);
        var styleEnum = Enum.Parse<HarmonyStyle>(style, ignoreCase: true);

        var chordSymbols = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chordList = chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))).ToList();
        while (chordList.Count < bars)
            chordList.AddRange(chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))));
        if (chordList.Count > bars)
            chordList = chordList.Take(bars).ToList();

        var progression = new ChordProgression { Key = keyObj, Chords = chordList };

        var melodySeq = melodyPattern.ToNoteSequence(0, 4);

        var generator = new HarmonyGenerator();
        var harmony = generator.Generate(new HarmonyOptions(keyObj, melodySeq, progression, styleEnum));
        var pattern = new Pattern(bars * 16, 1) { Name = $"Harmony ({style})" };
        pattern.ApplyNoteSequence(harmony, 0, "", 4);

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = patId, bars, style, noteCount = harmony.Events.Count(e => !e.IsRest) });
    }

    [McpServerTool(Name = "generate_arpeggio"), Description("Generate arpeggiated chord tones from a chord progression. Creates shimmering broken-chord patterns.")]
    public static string GenerateArpeggio(
        SessionState session,
        [Description("Chord progression as space-separated symbols, e.g. 'Am F C G'.")] string chords,
        [Description("Musical key.")] string key,
        [Description("Number of bars.")] int bars = 4,
        [Description("Octave for the arpeggio (3-6).")] int octave = 4,
        [Description("Note length in beats (0.25 = sixteenth, 0.5 = eighth, 1.0 = quarter).")] float noteLength = 0.25f)
    {
        var keyObj = Key.Parse(key);
        var chordSymbols = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chordList = chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))).ToList();
        while (chordList.Count < bars)
            chordList.AddRange(chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))));
        if (chordList.Count > bars)
            chordList = chordList.Take(bars).ToList();

        var progression = new ChordProgression { Key = keyObj, Chords = chordList };

        var generator = new ArpeggioPatternGenerator();
        var arp = generator.Generate(progression, octave, noteLength);
        var pattern = new Pattern(bars * 16, 1) { Name = "Arpeggio" };
        pattern.ApplyNoteSequence(arp, 0, "", 4);

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = patId, bars, octave, noteLength, noteCount = arp.Events.Count(e => !e.IsRest) });
    }

    [McpServerTool(Name = "generate_pad"), Description("Generate sustained chord tones from a progression. Creates warm, held notes that change with the chords.")]
    public static string GeneratePad(
        SessionState session,
        [Description("Chord progression as space-separated symbols, e.g. 'Am F C G'.")] string chords,
        [Description("Musical key.")] string key,
        [Description("Number of bars.")] int bars = 4,
        [Description("Octave (3-5).")] int octave = 4,
        [Description("Number of sustained pad voices/channels (1-2).")] int voices = 1)
    {
        var keyObj = Key.Parse(key);
        var chordSymbols = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chordList = chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))).ToList();
        while (chordList.Count < bars)
            chordList.AddRange(chordSymbols.Select(s => new ChordEvent(Chord.Parse(s))));
        if (chordList.Count > bars)
            chordList = chordList.Take(bars).ToList();

        var progression = new ChordProgression { Key = keyObj, Chords = chordList };

        var generator = new PadGenerator();
        voices = Math.Clamp(voices, 1, 2);
        var pattern = new Pattern(bars * 16, voices) { Name = voices == 1 ? "Pad" : "Pad Voicing" };
        int noteCount = 0;
        if (voices == 1)
        {
            var pad = generator.Generate(progression, beatsPerBar: 4, octave: octave);
            pattern.ApplyNoteSequence(pad, 0, "", 4);
            noteCount = pad.Events.Count(e => !e.IsRest);
        }
        else
        {
            var padVoices = generator.GenerateVoicings(progression, voices, beatsPerBar: 4, octave: octave);
            for (int channel = 0; channel < padVoices.Count; channel++)
            {
                pattern.ApplyNoteSequence(padVoices[channel], channel, "", 4);
                noteCount += padVoices[channel].Events.Count(e => !e.IsRest);
            }
        }

        string patId = session.AddPattern(pattern);
        return JsonSerializer.Serialize(new { patternId = patId, bars, octave, voices, noteCount });
    }

    [McpServerTool(Name = "get_pattern"), Description("Read the contents of a pattern — returns all non-empty cells with their row, channel, note, and volume.")]
    public static string GetPattern(
        SessionState session,
        [Description("Pattern ID.")] string patternId)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var cells = new List<object>();
        for (int r = 0; r < pattern.RowCount; r++)
            for (int c = 0; c < pattern.ChannelCount; c++)
            {
                var cell = pattern.GetCell(r, c);
                if (!cell.IsEmpty)
                    cells.Add(new { row = r, channel = c, note = cell.Note?.ToString(), volume = cell.Volume });
            }

        return JsonSerializer.Serialize(new
        {
            patternId, name = pattern.Name,
            rows = pattern.RowCount, channels = pattern.ChannelCount,
            cellCount = cells.Count, cells
        });
    }

    [McpServerTool(Name = "transpose_pattern"), Description("Shift all notes in a pattern up or down by a number of semitones. Creates a new pattern.")]
    public static string TransposePattern(
        SessionState session,
        [Description("Pattern ID to transpose.")] string patternId,
        [Description("Semitones to shift (positive = up, negative = down).")] int semitones,
        [Description("New pattern name.")] string? name = null)
    {
        var source = session.GetPattern(patternId);
        if (source == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var transposed = new Pattern(source.RowCount, source.ChannelCount)
        {
            Name = name ?? $"{source.Name} (+{semitones})"
        };

        int cellCount = 0;
        for (int r = 0; r < source.RowCount; r++)
            for (int c = 0; c < source.ChannelCount; c++)
            {
                var cell = source.GetCell(r, c);
                if (cell.IsEmpty) continue;

                if (cell.Note.HasValue && !cell.Note.Value.IsRest && !cell.Note.Value.IsCut)
                {
                    var newNote = cell.Note.Value.Transpose(semitones);
                    transposed.SetCell(r, c, new PatternCell(newNote, cell.InstrumentId, cell.Volume, cell.Effect));
                    cellCount++;
                }
                else
                {
                    transposed.SetCell(r, c, cell);
                }
            }

        foreach (var part in source.Parts)
            transposed.Parts.Add(ClonePart(part));

        string newId = session.AddPattern(transposed);
        return JsonSerializer.Serialize(new { patternId = newId, name = transposed.Name, semitones, cellsTransposed = cellCount });
    }

    [McpServerTool(Name = "copy_pattern"), Description("Duplicate a pattern so you can create variations without rebuilding from scratch.")]
    public static string CopyPattern(
        SessionState session,
        [Description("Pattern ID to copy.")] string patternId,
        [Description("Name for the copy.")] string? name = null)
    {
        var source = session.GetPattern(patternId);
        if (source == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var copy = new Pattern(source.RowCount, source.ChannelCount)
        {
            Name = name ?? $"{source.Name} (copy)"
        };

        for (int r = 0; r < source.RowCount; r++)
            for (int c = 0; c < source.ChannelCount; c++)
            {
                var cell = source.GetCell(r, c);
                if (!cell.IsEmpty)
                    copy.SetCell(r, c, cell);
            }

        foreach (var part in source.Parts)
            copy.Parts.Add(ClonePart(part));

        string newId = session.AddPattern(copy);
        return JsonSerializer.Serialize(new { patternId = newId, name = copy.Name, rows = copy.RowCount, channels = copy.ChannelCount });
    }

    [McpServerTool(Name = "merge_patterns"), Description("Combine multiple single-channel patterns into one multi-channel pattern. Each source pattern's channel 0 maps to a target channel.")]
    public static string MergePatterns(
        SessionState session,
        [Description("Comma-separated pattern IDs to merge, in channel order. E.g. 'pat_001,pat_002,pat_003' maps to channels 0,1,2.")] string patternIds,
        [Description("Name for the merged pattern.")] string? name = null)
    {
        var ids = patternIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0)
            return JsonSerializer.Serialize(new { error = "No pattern IDs provided." });

        var patterns = ids.Select(id => session.GetPattern(id)).ToList();
        for (int i = 0; i < patterns.Count; i++)
        {
            if (patterns[i] == null)
                return JsonSerializer.Serialize(new { error = $"Pattern '{ids[i]}' not found." });
        }

        int maxRows = patterns.Max(p => p!.RowCount);
        var merged = new Pattern(maxRows, patterns.Count)
        {
            Name = name ?? "Merged"
        };

        for (int ch = 0; ch < patterns.Count; ch++)
        {
            var source = patterns[ch]!;
            for (int r = 0; r < source.RowCount; r++)
            {
                var cell = source.GetCell(r, 0);
                if (!cell.IsEmpty)
                    merged.SetCell(r, ch, cell);
            }

            foreach (var part in source.Parts.Where(part => part.Channel == 0))
                merged.Parts.Add(ClonePart(part, ch));
        }

        string newId = session.AddPattern(merged);
        return JsonSerializer.Serialize(new { patternId = newId, name = merged.Name, rows = merged.RowCount, channels = merged.ChannelCount });
    }

    [McpServerTool(Name = "clear_channel"), Description("Clear all notes on a specific channel within a pattern.")]
    public static string ClearChannel(
        SessionState session,
        [Description("Pattern ID.")] string patternId,
        [Description("Channel index to clear (0-based).")] int channel)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });
        if (channel < 0 || channel >= pattern.ChannelCount)
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{pattern.ChannelCount - 1})." });

        int cleared = 0;
        for (int r = 0; r < pattern.RowCount; r++)
        {
            if (!pattern.GetCell(r, channel).IsEmpty)
            {
                pattern.ClearCell(r, channel);
                cleared++;
            }
        }

        return JsonSerializer.Serialize(new { patternId, channel, cellsCleared = cleared });
    }

    [McpServerTool(Name = "generate_chord_progression"), Description("Generate a chord progression suited to a mood and genre.")]
    public static string GenerateChordProgression(
        [Description("Musical key, e.g. 'Am', 'C'.")] string key,
        [Description("Mood: Heroic, Tense, Calm, Mysterious, Triumphant, Melancholy, Urgent, Playful, Dark, Epic.")] string mood = "Heroic",
        [Description("Genre: Action, RpgBattle, RpgTown, Platformer, Puzzle, Horror, Space, Fantasy, Sports.")] string genre = "Action",
        [Description("Number of bars.")] int bars = 4)
    {
        var keyObj = Key.Parse(key);
        var moodEnum = Enum.Parse<Mood>(mood, ignoreCase: true);
        var genreEnum = Enum.Parse<Genre>(genre, ignoreCase: true);

        var generator = new ChordProgressionGenerator();
        var progression = generator.Generate(new ProgressionOptions(keyObj, moodEnum, genreEnum, bars));

        return JsonSerializer.Serialize(new
        {
            key,
            template = progression.TemplateName,
            chords = progression.Chords.Select(c => c.Chord.ToString()).ToArray(),
            bars = progression.TotalBars,
            progression = progression.ToString()
        });
    }

    [McpServerTool(Name = "add_pattern_to_song"), Description("Add a pattern to a song's playback order.")]
    public static string AddPatternToSong(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Pattern ID to add.")] string patternId,
        [Description("Number of times to repeat.")] int repeat = 1)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });

        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        // Add pattern to song's pattern list if not already there
        int patIdx = song.Patterns.IndexOf(pattern);
        if (patIdx == -1)
        {
            song.Patterns.Add(pattern);
            patIdx = song.Patterns.Count - 1;
        }

        for (int i = 0; i < repeat; i++)
            song.AddToOrder(patIdx);

        return JsonSerializer.Serialize(new { songId, patternId, patternIndex = patIdx, repeat, totalOrders = song.OrderList.Entries.Count });
    }

    private static Part ClonePart(Part source, int? channelOverride = null) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        Name = source.Name,
        Channel = channelOverride ?? source.Channel,
        IsDrumPart = source.IsDrumPart,
        ProgramOverride = source.ProgramOverride == null
            ? null
            : new MidiProgram(
                source.ProgramOverride.ProgramNumber,
                source.ProgramOverride.Name,
                source.ProgramOverride.Category,
                source.ProgramOverride.DefaultVolume,
                source.ProgramOverride.DefaultPan,
                source.ProgramOverride.ReverbSend,
                source.ProgramOverride.ChorusSend,
                source.ProgramOverride.BankMsb,
                source.ProgramOverride.BankLsb),
        Notes = source.Notes
            .Select(note => new PartNote(note.Note, note.StartBeat, note.DurationBeats, note.Velocity))
            .ToList(),
        AutomationLanes = source.AutomationLanes
            .Select(lane => new AutomationLane
            {
                Type = lane.Type,
                Points = lane.Points.Select(point => new AutomationPoint(point.Beat, point.Value)).ToList()
            })
            .ToList()
    };

    private record NoteInput(int Row, int Channel, string Note, string? Instrument = null, int? Volume = null);
}
