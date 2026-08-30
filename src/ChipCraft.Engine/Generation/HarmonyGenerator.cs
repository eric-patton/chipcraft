using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

public enum HarmonyStyle
{
    ThirdsBelow,
    SixthsBelow,
    ArpeggiatedChords,
    Countermelody
}

public record HarmonyOptions(
    Key Key,
    NoteSequence Melody,
    ChordProgression Progression,
    HarmonyStyle Style = HarmonyStyle.ThirdsBelow,
    int BeatsPerBar = 4
);

/// <summary>
/// Generates harmony parts that complement a melody.
/// Can produce parallel harmony (3rds/6ths below), arpeggiated chord pads,
/// or simple countermelody lines.
/// </summary>
public class HarmonyGenerator
{
    private readonly Random _random;

    public HarmonyGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public NoteSequence Generate(HarmonyOptions options)
    {
        return options.Style switch
        {
            HarmonyStyle.ThirdsBelow => GenerateParallelHarmony(options, -3),
            HarmonyStyle.SixthsBelow => GenerateParallelHarmony(options, -4),
            HarmonyStyle.ArpeggiatedChords => GenerateArpeggiatedChords(options),
            HarmonyStyle.Countermelody => GenerateCountermelody(options),
            _ => GenerateParallelHarmony(options, -3)
        };
    }

    /// <summary>
    /// Generate harmony by transposing melody notes down by a scale interval.
    /// Adjusts intervals to stay within the scale (diatonic 3rds/6ths).
    /// </summary>
    private NoteSequence GenerateParallelHarmony(HarmonyOptions options, int scaleDegreeOffset)
    {
        var events = new List<NoteEvent>();
        var scale = options.Key.Scale;

        foreach (var melodyEvent in options.Melody.Events)
        {
            if (melodyEvent.IsRest)
            {
                events.Add(melodyEvent);
                continue;
            }

            // Find the melody note's scale degree, then offset by the interval
            int? degree = scale.GetDegreeOf(melodyEvent.Note);
            if (degree.HasValue)
            {
                int harmonyDegree = degree.Value + scaleDegreeOffset;
                // Wrap around the scale
                int intervals = scale.Intervals.Length;
                while (harmonyDegree < 1) harmonyDegree += intervals;

                int octaveShift = 0;
                if (harmonyDegree > intervals) { harmonyDegree -= intervals; octaveShift = 12; }

                var harmonyNote = scale.GetDegree(harmonyDegree, melodyEvent.Note.Octave);
                if (octaveShift != 0) harmonyNote = harmonyNote.Transpose(octaveShift);

                // Keep harmony below the melody
                while (harmonyNote.MidiNumber >= melodyEvent.Note.MidiNumber)
                    harmonyNote = harmonyNote.Transpose(-12);
                // But not too low
                if (harmonyNote.MidiNumber < 48) // C3
                    harmonyNote = harmonyNote.Transpose(12);

                events.Add(melodyEvent with { Note = harmonyNote, Velocity = melodyEvent.Velocity * 0.7f });
            }
            else
            {
                // Note not in scale, just shift down by semitones
                var shifted = melodyEvent.Note.Transpose(scaleDegreeOffset * 2);
                events.Add(melodyEvent with { Note = shifted, Velocity = melodyEvent.Velocity * 0.7f });
            }
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = options.Melody.TotalBars,
            BeatsPerBar = options.BeatsPerBar
        };
    }

    /// <summary>
    /// Generate arpeggiated chord patterns from the progression.
    /// Each chord is played as a cycling arpeggio.
    /// </summary>
    private NoteSequence GenerateArpeggiatedChords(HarmonyOptions options)
    {
        var events = new List<NoteEvent>();
        float currentBeat = 0;
        int bpb = options.BeatsPerBar;

        foreach (var chordEvent in options.Progression.Chords)
        {
            float barBeats = chordEvent.DurationBars * bpb;
            var chordNotes = chordEvent.Chord.GetNotes(4); // Octave 4

            int noteIdx = 0;
            bool ascending = true;
            for (float b = 0; b < barBeats; b += 0.5f)
            {
                var note = chordNotes[noteIdx];
                events.Add(new NoteEvent(note, currentBeat + b, 0.5f, 0.55f));

                if (ascending)
                {
                    noteIdx++;
                    if (noteIdx >= chordNotes.Length) { noteIdx = chordNotes.Length - 2; ascending = false; }
                    if (noteIdx < 0) noteIdx = 0;
                }
                else
                {
                    noteIdx--;
                    if (noteIdx < 0) { noteIdx = 1; ascending = true; }
                    if (noteIdx >= chordNotes.Length) noteIdx = chordNotes.Length - 1;
                }
            }

            currentBeat += barBeats;
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = options.Progression.TotalBars,
            BeatsPerBar = options.BeatsPerBar
        };
    }

    /// <summary>
    /// Generate a simple countermelody: when melody holds or rests, harmony plays;
    /// when melody is active, harmony rests or holds a chord tone.
    /// </summary>
    private NoteSequence GenerateCountermelody(HarmonyOptions options)
    {
        var events = new List<NoteEvent>();
        var chordToneMap = BuildChordTonesByBar(options.Progression, options.BeatsPerBar);
        float totalBeats = options.Melody.TotalBars * options.BeatsPerBar;

        // Build a set of beats where the melody is active
        var melodyActiveBeat = new HashSet<int>();
        foreach (var e in options.Melody.Events.Where(e => !e.IsRest))
        {
            for (float b = e.StartBeat; b < e.EndBeat; b += 0.5f)
                melodyActiveBeat.Add((int)(b * 2));
        }

        float currentBeat = 0;
        while (currentBeat < totalBeats)
        {
            int halfBeatIdx = (int)(currentBeat * 2);
            bool melodyActive = melodyActiveBeat.Contains(halfBeatIdx);

            int bar = (int)(currentBeat / options.BeatsPerBar);
            var chordNotes = chordToneMap.GetValueOrDefault(bar);

            if (!melodyActive && chordNotes != null && chordNotes.Length > 0)
            {
                // Play a chord tone when melody is quiet
                var note = chordNotes[_random.Next(chordNotes.Length)];
                float dur = _random.NextDouble() < 0.5 ? 1f : 0.5f;
                events.Add(new NoteEvent(note, currentBeat, dur, 0.5f));
                currentBeat += dur;
            }
            else
            {
                events.Add(new NoteEvent(Note.Rest, currentBeat, 0.5f));
                currentBeat += 0.5f;
            }
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = options.Melody.TotalBars,
            BeatsPerBar = options.BeatsPerBar
        };
    }

    private static Dictionary<int, Note[]> BuildChordTonesByBar(ChordProgression progression, int beatsPerBar)
    {
        var map = new Dictionary<int, Note[]>();
        int bar = 0;
        foreach (var ce in progression.Chords)
        {
            var notes = ce.Chord.GetNotes(4);
            for (int b = 0; b < ce.DurationBars; b++)
                map[bar + b] = notes;
            bar += ce.DurationBars;
        }
        return map;
    }
}
