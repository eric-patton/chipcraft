using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// Generates rhythmic accent/stab patterns. Accents play short chord hits
/// on off-beats or syncopated positions to add rhythmic drive and punch.
/// Think brass stabs in Genesis music or orchestral hits in SNES.
/// </summary>
public class AccentGenerator
{
    private readonly Random _random;

    public AccentGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public NoteSequence Generate(ChordProgression progression, float energy = 0.5f,
        int octave = 4, int beatsPerBar = 4)
    {
        var events = new List<NoteEvent>();
        float currentBeat = 0;

        foreach (var chordEvent in progression.Chords)
        {
            float barBeats = chordEvent.DurationBars * beatsPerBar;
            var notes = chordEvent.Chord.GetNotes(octave);
            var root = notes[0];

            // Place accents on rhythmically interesting positions
            for (float b = 0; b < barBeats; b += 0.5f)
            {
                float beatInBar = b % beatsPerBar;
                bool isAccentPosition = IsAccentBeat(beatInBar, energy);

                if (isAccentPosition && _random.NextDouble() < 0.6)
                {
                    // Pick root or 5th for a punchy stab
                    var note = notes.Length >= 3 && _random.NextDouble() < 0.3 ? notes[2] : root;
                    float vel = beatInBar < 0.01f ? 0.65f : 0.5f; // Stronger on downbeat
                    events.Add(new NoteEvent(note, currentBeat + b, 0.25f, vel));
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

    private static bool IsAccentBeat(float beatInBar, float energy)
    {
        // Low energy: only on "and of 2" and "and of 4" (off-beat accents)
        // High energy: add "and of 1", "and of 3", and some 16th positions
        if (MathF.Abs(beatInBar - 1.5f) < 0.01f) return true;  // "and of 2"
        if (MathF.Abs(beatInBar - 3.5f) < 0.01f) return true;  // "and of 4"

        if (energy > 0.5f)
        {
            if (MathF.Abs(beatInBar - 0.5f) < 0.01f) return true;  // "and of 1"
            if (MathF.Abs(beatInBar - 2.5f) < 0.01f) return true;  // "and of 3"
        }

        if (energy > 0.8f)
        {
            if (MathF.Abs(beatInBar - 0.75f) < 0.01f) return true;
            if (MathF.Abs(beatInBar - 2.75f) < 0.01f) return true;
        }

        return false;
    }
}
