using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// Generates arpeggiated chord patterns as note sequences.
/// Unlike the ArpeggioEngine (which cycles notes within a single voice),
/// this produces explicit note events for a dedicated arpeggio channel.
/// Creates the shimmering, cycling chord texture common in chiptune.
/// </summary>
public class ArpeggioPatternGenerator
{
    private readonly Random _random;

    public ArpeggioPatternGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public NoteSequence Generate(ChordProgression progression, int octave = 4,
        float noteLength = 0.25f, int beatsPerBar = 4, float velocity = 0.45f)
    {
        var events = new List<NoteEvent>();
        float currentBeat = 0;

        foreach (var chordEvent in progression.Chords)
        {
            float barBeats = chordEvent.DurationBars * beatsPerBar;
            var chordNotes = chordEvent.Chord.GetNotes(octave);

            // Arpeggio pattern: cycle up then down through chord tones
            int idx = 0;
            bool ascending = true;

            for (float b = 0; b < barBeats; b += noteLength)
            {
                var note = chordNotes[idx];
                // Alternate velocity for rhythmic interest
                float vel = (b % 1f < 0.01f) ? velocity : velocity * 0.75f;
                events.Add(new NoteEvent(note, currentBeat + b, noteLength, vel));

                if (ascending)
                {
                    idx++;
                    if (idx >= chordNotes.Length)
                    {
                        idx = Math.Max(0, chordNotes.Length - 2);
                        ascending = false;
                    }
                }
                else
                {
                    idx--;
                    if (idx < 0)
                    {
                        idx = Math.Min(1, chordNotes.Length - 1);
                        ascending = true;
                    }
                }
            }

            currentBeat += barBeats;
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = progression.TotalBars,
            BeatsPerBar = beatsPerBar
        };
    }
}
