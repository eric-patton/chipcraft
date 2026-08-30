using ChipCraft.Engine.Core;
using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Theory;

/// <summary>
/// A musical chord: root note + quality. Parses standard chord symbols
/// like "Am", "C", "F#dim", "G7", "Bbmaj7", "Dsus4".
/// </summary>
public class Chord
{
    public Note Root { get; }
    public ChordQuality Quality { get; }

    public Chord(Note root, ChordQuality quality)
    {
        Root = root;
        Quality = quality;
    }

    /// <summary>
    /// Get the notes of this chord in a specific octave.
    /// </summary>
    public Note[] GetNotes(int octave = 4)
    {
        int baseMidi = (octave + 1) * Constants.SemitonesPerOctave + Root.PitchClass;
        return ChordDatabase.GetIntervals(Quality)
            .Select(i => Note.FromMidi(baseMidi + i))
            .ToArray();
    }

    /// <summary>
    /// Get the note names (e.g., ["A", "C", "E"] for Am).
    /// </summary>
    public string[] GetNoteNames()
    {
        return GetNotes(4).Select(n => n.Name).ToArray();
    }

    /// <summary>
    /// Parse a chord symbol like "Am", "C", "F#dim", "G7", "Bbmaj7", "D5" (power chord).
    /// </summary>
    public static Chord Parse(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Chord symbol cannot be empty.", nameof(symbol));

        var span = symbol.AsSpan();
        int index = 0;

        // Parse root note letter
        int rootPc = char.ToUpperInvariant(span[index++]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new FormatException($"Invalid root note in chord '{symbol}'.")
        };

        // Parse optional accidental
        if (index < span.Length && span[index] == '#') { rootPc = (rootPc + 1) % 12; index++; }
        else if (index < span.Length && span[index] == 'b') { rootPc = (rootPc + 11) % 12; index++; }

        var root = Note.FromMidi(60 + rootPc); // Use octave 4 as reference

        // Parse quality suffix
        string suffix = index < span.Length ? span[index..].ToString() : "";
        var quality = ParseQuality(suffix);

        return new Chord(root, quality);
    }

    private static ChordQuality ParseQuality(string suffix) => suffix.ToLowerInvariant() switch
    {
        "" or "maj" or "major" => ChordQuality.Major,
        "m" or "min" or "minor" => ChordQuality.Minor,
        "dim" or "o" => ChordQuality.Diminished,
        "aug" or "+" => ChordQuality.Augmented,
        "sus2" => ChordQuality.Sus2,
        "sus4" or "sus" => ChordQuality.Sus4,
        "7" or "dom7" => ChordQuality.Dom7,
        "maj7" or "m7" when suffix.StartsWith("maj", StringComparison.OrdinalIgnoreCase) => ChordQuality.Maj7,
        "m7" or "min7" => ChordQuality.Min7,
        "dim7" or "o7" => ChordQuality.Dim7,
        "mmaj7" or "minmaj7" or "m/maj7" => ChordQuality.MinMaj7,
        "aug7" or "+7" => ChordQuality.Aug7,
        "9" or "dom9" => ChordQuality.Dom9,
        "m9" or "min9" => ChordQuality.Min9,
        "add9" => ChordQuality.Add9,
        "5" => ChordQuality.Power,
        _ => ChordQuality.Major // Default fallback
    };

    /// <summary>
    /// Returns the chord symbol string (e.g., "Am", "C", "F#dim").
    /// </summary>
    public override string ToString()
    {
        string suffix = Quality switch
        {
            ChordQuality.Major => "",
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            ChordQuality.Sus2 => "sus2",
            ChordQuality.Sus4 => "sus4",
            ChordQuality.Dom7 => "7",
            ChordQuality.Maj7 => "maj7",
            ChordQuality.Min7 => "m7",
            ChordQuality.Dim7 => "dim7",
            ChordQuality.MinMaj7 => "mmaj7",
            ChordQuality.Aug7 => "aug7",
            ChordQuality.Dom9 => "9",
            ChordQuality.Min9 => "m9",
            ChordQuality.Add9 => "add9",
            ChordQuality.Power => "5",
            _ => ""
        };
        return $"{Root.Name}{suffix}";
    }
}
