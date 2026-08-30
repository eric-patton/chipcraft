using ChipCraft.Engine.Core;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

public record BassLineOptions(
    ChordProgression Progression,
    BassStyle Style = BassStyle.RootFifth,
    int Octave = 2,
    int BeatsPerBar = 4,
    float Energy = 0.5f
);

/// <summary>
/// Generates bass lines from chord progressions in 5 styles.
/// Bass lines provide the harmonic foundation and rhythmic drive.
/// </summary>
public class BassLineGenerator
{
    private readonly Random _random;

    public BassLineGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public NoteSequence Generate(BassLineOptions options)
    {
        var events = new List<NoteEvent>();
        float currentBeat = 0;
        int bpb = options.BeatsPerBar;

        foreach (var chordEvent in options.Progression.Chords)
        {
            float barBeats = chordEvent.DurationBars * bpb;
            var chordNotes = chordEvent.Chord.GetNotes(options.Octave);
            var root = chordNotes[0];
            var fifth = chordNotes.Length >= 3 ? chordNotes[2] : chordNotes[^1];

            var barEvents = options.Style switch
            {
                BassStyle.RootFifth => GenerateRootFifth(root, fifth, currentBeat, barBeats, bpb),
                BassStyle.Octave => GenerateOctave(root, currentBeat, barBeats, bpb),
                BassStyle.Walking => GenerateWalking(chordNotes, root, currentBeat, barBeats, bpb, options),
                BassStyle.Pedal => GeneratePedal(root, currentBeat, barBeats, bpb),
                BassStyle.Arpeggiated => GenerateArpeggiated(chordNotes, currentBeat, barBeats, bpb),
                _ => GenerateRootFifth(root, fifth, currentBeat, barBeats, bpb)
            };

            events.AddRange(barEvents);
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
    /// Root on beat 1, fifth on beat 3. Classic rock/game bass.
    /// </summary>
    private List<NoteEvent> GenerateRootFifth(Note root, Note fifth, float startBeat, float barBeats, int bpb)
    {
        var events = new List<NoteEvent>();
        for (float b = 0; b < barBeats; b += bpb)
        {
            events.Add(new NoteEvent(root, startBeat + b, 1f, 0.85f));
            if (bpb >= 4)
                events.Add(new NoteEvent(fifth, startBeat + b + 2f, 1f, 0.75f));
        }
        return events;
    }

    /// <summary>
    /// Root, then octave root. Driving, energetic feel.
    /// </summary>
    private List<NoteEvent> GenerateOctave(Note root, float startBeat, float barBeats, int bpb)
    {
        var octaveUp = root.Transpose(12);
        var events = new List<NoteEvent>();
        for (float b = 0; b < barBeats; b += bpb)
        {
            events.Add(new NoteEvent(root, startBeat + b, 1f, 0.85f));
            if (bpb >= 4)
            {
                events.Add(new NoteEvent(octaveUp, startBeat + b + 2f, 0.5f, 0.7f));
                events.Add(new NoteEvent(root, startBeat + b + 3f, 0.5f, 0.6f));
            }
        }
        return events;
    }

    /// <summary>
    /// Walking bass: stepwise motion through chord tones and passing tones, one note per beat.
    /// </summary>
    private List<NoteEvent> GenerateWalking(Note[] chordNotes, Note root, float startBeat, float barBeats, int bpb, BassLineOptions options)
    {
        var events = new List<NoteEvent>();
        var scale = options.Progression.Key.Scale;
        var scaleNotes = scale.GetNotesInRange(
            root.Transpose(-2), root.Transpose(14));

        Note current = root;
        for (int beat = 0; beat < (int)barBeats; beat++)
        {
            events.Add(new NoteEvent(current, startBeat + beat, 1f, beat == 0 ? 0.85f : 0.7f));

            // Next note: prefer stepwise, target chord tones on strong beats
            if (beat < (int)barBeats - 1)
            {
                var candidates = scaleNotes
                    .Where(n => Math.Abs(n.MidiNumber - current.MidiNumber) <= 4)
                    .Where(n => n.MidiNumber != current.MidiNumber)
                    .ToArray();

                if (candidates.Length > 0)
                {
                    // Prefer small intervals
                    var ordered = candidates.OrderBy(n => Math.Abs(n.MidiNumber - current.MidiNumber)).ToArray();
                    current = ordered[_random.Next(Math.Min(3, ordered.Length))];
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Pedal: sustained root note, re-articulated on beat 1.
    /// </summary>
    private static List<NoteEvent> GeneratePedal(Note root, float startBeat, float barBeats, int bpb)
    {
        return [new NoteEvent(root, startBeat, barBeats, 0.8f)];
    }

    /// <summary>
    /// Arpeggiated: cycle through chord tones in eighth notes.
    /// </summary>
    private List<NoteEvent> GenerateArpeggiated(Note[] chordNotes, float startBeat, float barBeats, int bpb)
    {
        var events = new List<NoteEvent>();
        int noteIndex = 0;
        bool ascending = true;

        for (float b = 0; b < barBeats; b += 0.5f)
        {
            var note = chordNotes[noteIndex];
            events.Add(new NoteEvent(note, startBeat + b, 0.5f, 0.7f));

            if (ascending)
            {
                noteIndex++;
                if (noteIndex >= chordNotes.Length)
                {
                    noteIndex = chordNotes.Length - 2;
                    ascending = false;
                    if (noteIndex < 0) noteIndex = 0;
                }
            }
            else
            {
                noteIndex--;
                if (noteIndex < 0)
                {
                    noteIndex = 1;
                    ascending = true;
                    if (noteIndex >= chordNotes.Length) noteIndex = 0;
                }
            }
        }

        return events;
    }
}
