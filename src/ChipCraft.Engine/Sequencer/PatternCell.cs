using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Sequencer;

/// <summary>
/// A single cell in the pattern grid: what to play in one channel at one row.
/// Null fields mean "carry forward previous state" (like a tracker).
/// </summary>
public record PatternCell(
    Note? Note = null,
    string? InstrumentId = null,
    byte? Volume = null,
    EffectCommand? Effect = null
)
{
    public static readonly PatternCell Empty = new();
    public bool IsEmpty => Note is null && InstrumentId is null && Volume is null && (Effect is null || Effect.IsEmpty);
}
