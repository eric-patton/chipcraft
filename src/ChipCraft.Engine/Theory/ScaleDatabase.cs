namespace ChipCraft.Engine.Theory;

/// <summary>
/// Static lookup of interval patterns (semitones from root) for all scale types.
/// </summary>
public static class ScaleDatabase
{
    private static readonly Dictionary<ScaleType, int[]> Intervals = new()
    {
        [ScaleType.Major]           = [0, 2, 4, 5, 7, 9, 11],
        [ScaleType.NaturalMinor]    = [0, 2, 3, 5, 7, 8, 10],
        [ScaleType.HarmonicMinor]   = [0, 2, 3, 5, 7, 8, 11],
        [ScaleType.MelodicMinor]    = [0, 2, 3, 5, 7, 9, 11],
        [ScaleType.PentatonicMajor] = [0, 2, 4, 7, 9],
        [ScaleType.PentatonicMinor] = [0, 3, 5, 7, 10],
        [ScaleType.Blues]           = [0, 3, 5, 6, 7, 10],
        [ScaleType.Dorian]          = [0, 2, 3, 5, 7, 9, 10],
        [ScaleType.Mixolydian]      = [0, 2, 4, 5, 7, 9, 10],
        [ScaleType.Phrygian]        = [0, 1, 3, 5, 7, 8, 10],
        [ScaleType.Lydian]          = [0, 2, 4, 6, 7, 9, 11],
        [ScaleType.Locrian]         = [0, 1, 3, 5, 6, 8, 10],
        [ScaleType.WholeTone]       = [0, 2, 4, 6, 8, 10],
        [ScaleType.Chromatic]       = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        [ScaleType.Diminished]      = [0, 2, 3, 5, 6, 8, 9, 11],
    };

    public static int[] GetIntervals(ScaleType type) =>
        Intervals.TryGetValue(type, out var intervals)
            ? intervals
            : throw new ArgumentOutOfRangeException(nameof(type));

    public static int GetDegreeCount(ScaleType type) => GetIntervals(type).Length;

    /// <summary>
    /// Returns true if the scale type is minor-sounding (has a minor 3rd).
    /// </summary>
    public static bool IsMinor(ScaleType type) => type switch
    {
        ScaleType.NaturalMinor or ScaleType.HarmonicMinor or ScaleType.MelodicMinor
            or ScaleType.PentatonicMinor or ScaleType.Blues or ScaleType.Dorian
            or ScaleType.Phrygian or ScaleType.Locrian or ScaleType.Diminished => true,
        _ => false
    };
}
