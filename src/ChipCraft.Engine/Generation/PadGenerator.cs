using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// Generates sustained chord pad parts. Pads play long held chord tones
/// that provide harmonic warmth and fill out the arrangement.
/// Changes notes only when the chord changes.
/// </summary>
public class PadGenerator
{
    public NoteSequence Generate(ChordProgression progression, int beatsPerBar = 4, int octave = 4)
    {
        var events = new List<NoteEvent>();
        float currentBeat = 0;

        foreach (var chordEvent in progression.Chords)
        {
            float duration = chordEvent.DurationBars * beatsPerBar;
            var notes = chordEvent.Chord.GetNotes(octave);

            // Play the 3rd of the chord (warmer than root, more harmonic than 5th)
            var padNote = notes.Length >= 2 ? notes[1] : notes[0];
            events.Add(new NoteEvent(padNote, currentBeat, duration, 0.45f));

            currentBeat += duration;
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = progression.TotalBars,
            BeatsPerBar = beatsPerBar
        };
    }

    public IReadOnlyList<NoteSequence> GenerateVoicings(
        ChordProgression progression,
        int voiceCount = 2,
        int beatsPerBar = 4,
        int octave = 4)
    {
        voiceCount = Math.Clamp(voiceCount, 1, 3);
        var voices = Enumerable.Range(0, voiceCount)
            .Select(_ => new List<NoteEvent>())
            .ToArray();

        float currentBeat = 0;
        foreach (var chordEvent in progression.Chords)
        {
            float duration = chordEvent.DurationBars * beatsPerBar;
            var notes = chordEvent.Chord.GetNotes(octave);
            var chordTones = new List<Models.Note>
            {
                notes[0].Transpose(-12),
                notes.Length >= 2 ? notes[1] : notes[0],
                notes.Length >= 3 ? notes[2] : notes[^1]
            };

            for (int voice = 0; voice < voiceCount; voice++)
            {
                var note = chordTones[Math.Min(voice, chordTones.Count - 1)];
                float velocity = voice == 0 ? 0.42f : 0.34f - (voice * 0.03f);
                voices[voice].Add(new NoteEvent(note, currentBeat, duration, velocity));
            }

            currentBeat += duration;
        }

        return voices.Select(events => new NoteSequence
        {
            Events = events,
            TotalBars = progression.TotalBars,
            BeatsPerBar = beatsPerBar
        }).ToList();
    }
}
