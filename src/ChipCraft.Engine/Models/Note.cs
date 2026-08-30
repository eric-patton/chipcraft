using ChipCraft.Engine.Core;

namespace ChipCraft.Engine.Models;

/// <summary>
/// Immutable musical note in scientific pitch notation (e.g., C4, A#3, Db5).
/// Converts between note name, MIDI number, and frequency.
/// </summary>
public readonly record struct Note(int MidiNumber) : IComparable<Note>
{
    private static readonly string[] SharpNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] FlatNames = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public static readonly Note Rest = new(-1);
    public static readonly Note Cut = new(-2);

    public int PitchClass => IsRest || IsCut ? -1 : ((MidiNumber % Constants.SemitonesPerOctave) + Constants.SemitonesPerOctave) % Constants.SemitonesPerOctave;
    public int Octave => IsRest || IsCut ? -1 : MidiNumber / Constants.SemitonesPerOctave - 1;
    public double Frequency => IsRest || IsCut ? 0.0 : MathUtils.MidiToFrequency(MidiNumber);
    public bool IsRest => MidiNumber == -1;
    public bool IsCut => MidiNumber == -2;
    public string Name => IsRest ? "---" : IsCut ? "===" : SharpNames[PitchClass];

    /// <summary>
    /// Parse a note from scientific pitch notation: "C4", "F#5", "Bb3", "---" (rest), "===" (cut).
    /// </summary>
    public static Note Parse(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation))
            throw new ArgumentException("Note notation cannot be empty.", nameof(notation));

        if (notation is "---" or "..." or "rest")
            return Rest;
        if (notation is "===" or "cut" or "off")
            return Cut;

        var span = notation.AsSpan();
        int index = 0;

        // Parse letter (C-G, case insensitive)
        int basePitch = char.ToUpperInvariant(span[index++]) switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new FormatException($"Invalid note letter in '{notation}'.")
        };

        // Parse optional accidental (# or b)
        int accidental = 0;
        if (index < span.Length && span[index] == '#')
        {
            accidental = 1;
            index++;
        }
        else if (index < span.Length && span[index] == 'b')
        {
            accidental = -1;
            index++;
        }

        // Parse octave number
        if (index >= span.Length || !int.TryParse(span[index..], out int octave))
            throw new FormatException($"Invalid octave in '{notation}'.");

        int midi = (octave + 1) * Constants.SemitonesPerOctave + basePitch + accidental;
        if (midi < Constants.MinMidiNumber || midi > Constants.MaxMidiNumber)
            throw new ArgumentOutOfRangeException(nameof(notation), $"MIDI number {midi} out of range for '{notation}'.");

        return new Note(midi);
    }

    public static Note FromMidi(int midiNumber) => new(midiNumber);

    public Note Transpose(int semitones) => IsRest || IsCut ? this : new Note(MidiNumber + semitones);

    public int CompareTo(Note other) => MidiNumber.CompareTo(other.MidiNumber);

    public override string ToString()
    {
        if (IsRest) return "---";
        if (IsCut) return "===";
        return $"{SharpNames[PitchClass]}{Octave}";
    }

    public string ToStringFlat()
    {
        if (IsRest) return "---";
        if (IsCut) return "===";
        return $"{FlatNames[PitchClass]}{Octave}";
    }
}
