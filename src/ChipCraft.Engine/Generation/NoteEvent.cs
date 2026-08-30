using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// A single note event in a generated sequence.
/// Used as the common output format for all generators (melody, bass, drums).
/// </summary>
public record NoteEvent(
    Note Note,
    float StartBeat,
    float DurationBeats,
    float Velocity = 0.8f
)
{
    public float EndBeat => StartBeat + DurationBeats;
    public bool IsRest => Note.IsRest;
}

/// <summary>
/// A sequence of note events on a single channel, with metadata.
/// </summary>
public class NoteSequence
{
    public List<NoteEvent> Events { get; init; } = [];
    public int TotalBars { get; init; }
    public int BeatsPerBar { get; init; } = 4;
    public float TotalBeats => TotalBars * BeatsPerBar;

    /// <summary>
    /// Get all events within a specific bar (0-indexed).
    /// </summary>
    public IEnumerable<NoteEvent> GetBar(int barIndex)
    {
        float barStart = barIndex * BeatsPerBar;
        float barEnd = barStart + BeatsPerBar;
        return Events.Where(e => e.StartBeat >= barStart && e.StartBeat < barEnd);
    }
}
