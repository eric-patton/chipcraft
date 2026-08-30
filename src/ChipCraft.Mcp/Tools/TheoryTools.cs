using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Theory;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class TheoryTools
{
    [McpServerTool(Name = "get_scale"), Description("Get all notes in a musical scale. Returns note names and intervals.")]
    public static string GetScale(
        [Description("Root note, e.g. 'C', 'F#', 'Bb'.")] string root,
        [Description("Scale type: Major, NaturalMinor, HarmonicMinor, PentatonicMajor, PentatonicMinor, Blues, Dorian, Mixolydian, Phrygian, Lydian, Locrian, WholeTone, Chromatic, Diminished.")] string scaleType = "NaturalMinor")
    {
        var type = Enum.Parse<ScaleType>(scaleType, ignoreCase: true);
        var scale = new Scale(root, type);
        var notes = scale.GetNoteNames();
        var intervals = ScaleDatabase.GetIntervals(type);

        return JsonSerializer.Serialize(new
        {
            root,
            scaleType = type.ToString(),
            notes,
            intervals,
            isMinor = ScaleDatabase.IsMinor(type),
            degreeCount = notes.Length
        });
    }

    [McpServerTool(Name = "get_chord"), Description("Get the notes in a chord from a chord symbol. Examples: 'Am', 'C', 'F#dim', 'G7', 'Bbmaj7'.")]
    public static string GetChord(
        [Description("Chord symbol, e.g. 'Am', 'C', 'F#dim', 'G7', 'Dsus4'.")] string symbol)
    {
        var chord = Chord.Parse(symbol);
        var notes = chord.GetNoteNames();

        return JsonSerializer.Serialize(new
        {
            symbol = chord.ToString(),
            root = chord.Root.Name,
            quality = chord.Quality.ToString(),
            notes
        });
    }

    [McpServerTool(Name = "suggest_progression"), Description("Suggest chord progressions that fit a mood and genre. Returns multiple ranked options.")]
    public static string SuggestProgression(
        [Description("Musical key, e.g. 'Am', 'C', 'Dm'.")] string key,
        [Description("Desired mood: Heroic, Tense, Calm, Mysterious, Triumphant, Melancholy, Urgent, Playful, Dark, Epic.")] string mood = "Heroic",
        [Description("Game genre: Action, RpgBattle, RpgTown, Platformer, Puzzle, Horror, Space, Fantasy, Sports.")] string genre = "Action",
        [Description("Number of suggestions (1-5).")] int count = 3)
    {
        var keyObj = Key.Parse(key);
        var moodEnum = Enum.Parse<Mood>(mood, ignoreCase: true);
        var genreEnum = Enum.Parse<Genre>(genre, ignoreCase: true);

        var generator = new ChordProgressionGenerator();
        var results = generator.GenerateMultiple(
            new ProgressionOptions(keyObj, moodEnum, genreEnum, Bars: 4),
            count);

        var suggestions = results.Select(p => new
        {
            name = p.TemplateName,
            chords = p.Chords.Select(c => c.Chord.ToString()).ToArray(),
            progression = p.ToString()
        });

        return JsonSerializer.Serialize(new { key, mood, genre, suggestions });
    }

    [McpServerTool(Name = "get_key_info"), Description("Get comprehensive information about a musical key: relative keys, diatonic chords, mood associations.")]
    public static string GetKeyInfo(
        [Description("Key name, e.g. 'Am', 'C', 'F#m'.")] string key)
    {
        var keyObj = Key.Parse(key);
        var relative = keyObj.GetRelativeKey();
        var parallel = keyObj.GetParallelKey();

        var diatonicChords = new List<string>();
        int degreeCount = ScaleDatabase.GetDegreeCount(keyObj.ScaleType);
        for (int i = 1; i <= Math.Min(degreeCount, 7); i++)
        {
            try { diatonicChords.Add(keyObj.GetDiatonicChord(i).ToString()); }
            catch { break; }
        }

        return JsonSerializer.Serialize(new
        {
            key = keyObj.ToString(),
            root = keyObj.Root.Name,
            scaleType = keyObj.ScaleType.ToString(),
            isMinor = keyObj.IsMinor,
            scaleNotes = keyObj.Scale.GetNoteNames(),
            diatonicChords,
            relativeKey = relative.ToString(),
            parallelKey = parallel.ToString(),
            defaultTempo = ProgressionDatabase.GetDefaultTempo(Genre.Action),
            suggestedMoods = keyObj.IsMinor
                ? new[] { "Heroic", "Tense", "Dark", "Melancholy", "Epic" }
                : new[] { "Calm", "Triumphant", "Playful", "Mysterious" }
        });
    }

    [McpServerTool(Name = "get_note_info"), Description("Get detailed info about a note: MIDI number, frequency, pitch class, octave, and which common scales contain it.")]
    public static string GetNoteInfo(
        [Description("Note name, e.g. 'C4', 'F#5', 'Bb3'.")] string note)
    {
        var n = Engine.Models.Note.Parse(note);

        // Check which common scales contain this note
        var commonKeys = new[] { "C", "G", "D", "A", "E", "F", "Bb",
                                 "Am", "Em", "Bm", "F#m", "Dm", "Gm", "Cm" };
        var inScales = new List<string>();
        foreach (var keyName in commonKeys)
        {
            var key = Key.Parse(keyName);
            var scale = key.Scale;
            if (scale.Contains(n))
                inScales.Add(keyName);
        }

        return JsonSerializer.Serialize(new
        {
            note = n.ToString(),
            midiNumber = n.MidiNumber,
            frequency = Math.Round(n.Frequency, 2),
            pitchClass = n.PitchClass,
            octave = n.Octave,
            inScales
        });
    }
}
