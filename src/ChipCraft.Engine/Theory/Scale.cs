using ChipCraft.Engine.Core;
using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Theory;

/// <summary>
/// A musical scale: root note + scale type. Provides note lookups,
/// range queries, and degree-based access.
/// </summary>
public class Scale
{
    public Note Root { get; }
    public ScaleType Type { get; }
    public int[] Intervals { get; }

    public Scale(Note root, ScaleType type)
    {
        Root = root;
        Type = type;
        Intervals = ScaleDatabase.GetIntervals(type);
    }

    public Scale(string rootName, ScaleType type)
        : this(Note.Parse(rootName + "4"), type)
    {
    }

    /// <summary>
    /// Get all notes of this scale in a specific octave.
    /// </summary>
    public Note[] GetNotes(int octave)
    {
        int baseMidi = (octave + 1) * Constants.SemitonesPerOctave + Root.PitchClass;
        return Intervals.Select(i => Note.FromMidi(baseMidi + i)).ToArray();
    }

    /// <summary>
    /// Get all scale notes within a MIDI note range (inclusive).
    /// </summary>
    public Note[] GetNotesInRange(Note low, Note high)
    {
        var notes = new List<Note>();
        int rootPc = Root.PitchClass;

        for (int midi = low.MidiNumber; midi <= high.MidiNumber; midi++)
        {
            int pc = ((midi % 12) - rootPc + 12) % 12;
            if (Intervals.Contains(pc))
                notes.Add(Note.FromMidi(midi));
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Get the note at a specific scale degree (1-indexed).
    /// Degree 1 = root. Supports octave wrapping (degree 8 = root+octave).
    /// </summary>
    public Note GetDegree(int degree, int octave = 4)
    {
        if (degree < 1)
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be >= 1.");

        int index = (degree - 1) % Intervals.Length;
        int octaveOffset = (degree - 1) / Intervals.Length;
        int baseMidi = (octave + 1) * Constants.SemitonesPerOctave + Root.PitchClass;

        return Note.FromMidi(baseMidi + Intervals[index] + octaveOffset * 12);
    }

    /// <summary>
    /// Check if a note's pitch class belongs to this scale.
    /// </summary>
    public bool Contains(Note note)
    {
        if (note.IsRest || note.IsCut) return false;
        int pc = ((note.PitchClass - Root.PitchClass) + 12) % 12;
        return Intervals.Contains(pc);
    }

    /// <summary>
    /// Get the scale degree (1-indexed) of a note, or null if not in scale.
    /// </summary>
    public int? GetDegreeOf(Note note)
    {
        if (note.IsRest || note.IsCut) return null;
        int pc = ((note.PitchClass - Root.PitchClass) + 12) % 12;
        int index = Array.IndexOf(Intervals, pc);
        return index >= 0 ? index + 1 : null;
    }

    /// <summary>
    /// Get all note names in this scale (e.g., ["C", "D", "E", "F", "G", "A", "B"]).
    /// </summary>
    public string[] GetNoteNames()
    {
        return GetNotes(4).Select(n => n.Name).ToArray();
    }

    public override string ToString() => $"{Root.Name} {Type}";
}
