using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Theory;

/// <summary>
/// A musical key: root note + scale type. Provides access to the scale,
/// diatonic chords, relative keys, and common progressions.
/// </summary>
public class Key
{
    public Note Root { get; }
    public ScaleType ScaleType { get; }
    public Scale Scale { get; }
    public bool IsMinor => ScaleDatabase.IsMinor(ScaleType);

    public Key(Note root, ScaleType scaleType)
    {
        Root = root;
        ScaleType = scaleType;
        Scale = new Scale(root, scaleType);
    }

    /// <summary>
    /// Parse a key string like "Am", "C", "F#m", "Bb", "Dm".
    /// Lowercase 'm' suffix = natural minor. No suffix = major.
    /// </summary>
    public static Key Parse(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            throw new ArgumentException("Key string cannot be empty.", nameof(keyString));

        var span = keyString.AsSpan();
        int index = 0;

        int rootPc = char.ToUpperInvariant(span[index++]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new FormatException($"Invalid key: '{keyString}'.")
        };

        if (index < span.Length && span[index] == '#') { rootPc = (rootPc + 1) % 12; index++; }
        else if (index < span.Length && span[index] == 'b') { rootPc = (rootPc + 11) % 12; index++; }

        var root = Note.FromMidi(60 + rootPc);
        string suffix = index < span.Length ? span[index..].ToString().ToLowerInvariant() : "";

        var scaleType = suffix switch
        {
            "m" or "min" or "minor" => ScaleType.NaturalMinor,
            "hm" or "harm" => ScaleType.HarmonicMinor,
            "mm" or "melmin" => ScaleType.MelodicMinor,
            "dor" or "dorian" => ScaleType.Dorian,
            "mix" or "mixolydian" => ScaleType.Mixolydian,
            "phr" or "phrygian" => ScaleType.Phrygian,
            "lyd" or "lydian" => ScaleType.Lydian,
            "loc" or "locrian" => ScaleType.Locrian,
            _ => ScaleType.Major
        };

        return new Key(root, scaleType);
    }

    /// <summary>
    /// Get the relative major key (for minor keys) or relative minor (for major keys).
    /// </summary>
    public Key GetRelativeKey()
    {
        if (IsMinor)
        {
            // Relative major is 3 semitones up from minor root
            var majorRoot = Root.Transpose(3);
            return new Key(Note.FromMidi(60 + majorRoot.PitchClass), ScaleType.Major);
        }
        else
        {
            // Relative minor is 3 semitones down from major root
            var minorRoot = Root.Transpose(-3);
            return new Key(Note.FromMidi(60 + minorRoot.PitchClass), ScaleType.NaturalMinor);
        }
    }

    /// <summary>
    /// Get the parallel key (same root, opposite major/minor).
    /// </summary>
    public Key GetParallelKey()
    {
        return IsMinor
            ? new Key(Root, ScaleType.Major)
            : new Key(Root, ScaleType.NaturalMinor);
    }

    /// <summary>
    /// Build the diatonic chord for a given scale degree (1-7).
    /// Returns a triad built by stacking thirds within the scale.
    /// </summary>
    public Chord GetDiatonicChord(int degree)
    {
        if (degree < 1 || degree > Scale.Intervals.Length)
            throw new ArgumentOutOfRangeException(nameof(degree));

        var intervals = Scale.Intervals;
        int rootInterval = intervals[degree - 1];
        int thirdIndex = (degree - 1 + 2) % intervals.Length;
        int fifthIndex = (degree - 1 + 4) % intervals.Length;

        int thirdInterval = intervals[thirdIndex];
        int fifthInterval = intervals[fifthIndex];

        // Calculate intervals relative to chord root, wrapping at octave
        int third = (thirdInterval - rootInterval + 12) % 12;
        int fifth = (fifthInterval - rootInterval + 12) % 12;

        var chordRoot = Note.FromMidi(60 + (Root.PitchClass + rootInterval) % 12);

        var quality = (third, fifth) switch
        {
            (4, 7) => ChordQuality.Major,
            (3, 7) => ChordQuality.Minor,
            (3, 6) => ChordQuality.Diminished,
            (4, 8) => ChordQuality.Augmented,
            _ => third <= 3 ? ChordQuality.Minor : ChordQuality.Major
        };

        return new Chord(chordRoot, quality);
    }

    public override string ToString()
    {
        string suffix = IsMinor ? "m" : "";
        return $"{Root.Name}{suffix}";
    }
}
