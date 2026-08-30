namespace ChipCraft.Engine.Theory;

/// <summary>
/// Static lookup of interval patterns (semitones from root) for all chord qualities.
/// </summary>
public static class ChordDatabase
{
    private static readonly Dictionary<ChordQuality, int[]> Intervals = new()
    {
        [ChordQuality.Major]      = [0, 4, 7],
        [ChordQuality.Minor]      = [0, 3, 7],
        [ChordQuality.Diminished] = [0, 3, 6],
        [ChordQuality.Augmented]  = [0, 4, 8],
        [ChordQuality.Sus2]       = [0, 2, 7],
        [ChordQuality.Sus4]       = [0, 5, 7],
        [ChordQuality.Dom7]       = [0, 4, 7, 10],
        [ChordQuality.Maj7]       = [0, 4, 7, 11],
        [ChordQuality.Min7]       = [0, 3, 7, 10],
        [ChordQuality.Dim7]       = [0, 3, 6, 9],
        [ChordQuality.MinMaj7]    = [0, 3, 7, 11],
        [ChordQuality.Aug7]       = [0, 4, 8, 10],
        [ChordQuality.Dom9]       = [0, 4, 7, 10, 14],
        [ChordQuality.Min9]       = [0, 3, 7, 10, 14],
        [ChordQuality.Add9]       = [0, 4, 7, 14],
        [ChordQuality.Power]      = [0, 7],
    };

    public static int[] GetIntervals(ChordQuality quality) =>
        Intervals.TryGetValue(quality, out var intervals)
            ? intervals
            : throw new ArgumentOutOfRangeException(nameof(quality));

    public static int GetNoteCount(ChordQuality quality) => GetIntervals(quality).Length;
}
