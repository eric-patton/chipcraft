using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// Options for melody generation.
/// </summary>
public record MelodyOptions(
    Key Key,
    MelodyContour Contour = MelodyContour.Arch,
    int Bars = 4,
    int BeatsPerBar = 4,
    Note? LowNote = null,
    Note? HighNote = null,
    float MinNoteDuration = 0.5f,
    float MaxNoteDuration = 2f,
    float RestProbability = 0.1f,
    float Energy = 0.5f,
    ChordProgression? Progression = null,
    float StepBias = 0.55f,
    float LeapSize = 3f
);

/// <summary>
/// Generates melodies using a constrained Markov chain on scale degrees.
/// When a ChordProgression is provided, strongly biases toward chord tones
/// on strong beats, using non-chord tones as passing/neighbor tones on weak beats.
/// </summary>
public class MelodyGenerator
{
    private readonly Random _random;

    private const float DefaultSmallLeapProb = 0.30f;

    public MelodyGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public NoteSequence Generate(MelodyOptions options)
    {
        var key = options.Key;
        var low = options.LowNote ?? Note.Parse("C4");
        var high = options.HighNote ?? Note.Parse("C6");
        var scaleNotes = key.Scale.GetNotesInRange(low, high);

        if (scaleNotes.Length < 3)
            throw new ArgumentException("Range too narrow for melody generation.");

        // Build chord tone lookup: beat -> set of chord tone pitch classes
        var chordToneMap = BuildChordToneMap(options);

        var events = new List<NoteEvent>();
        float totalBeats = options.Bars * options.BeatsPerBar;

        var rhythm = GenerateRhythm(options);

        int currentIndex = GetStartingIndex(scaleNotes, options.Contour);
        bool leapRecoveryNeeded = false;
        int leapDirection = 0;

        float currentBeat = 0;
        foreach (float duration in rhythm)
        {
            if (_random.NextDouble() < options.RestProbability && currentBeat > 0)
            {
                events.Add(new NoteEvent(Note.Rest, currentBeat, duration));
                currentBeat += duration;
                continue;
            }

            float t = currentBeat / totalBeats;
            float contourTarget = GetContourTarget(t, options.Contour);

            // Is this a strong beat? (beat 1 or 3 in 4/4)
            bool isStrongBeat = (currentBeat % options.BeatsPerBar) < 0.01f ||
                                MathF.Abs(currentBeat % options.BeatsPerBar - 2f) < 0.01f;

            // Get chord tones for this beat
            HashSet<int>? chordTonePcs = null;
            if (chordToneMap != null)
            {
                int bar = (int)(currentBeat / options.BeatsPerBar);
                if (chordToneMap.TryGetValue(bar, out var pcs))
                    chordTonePcs = pcs;
            }

            int nextIndex = PickNextIndex(
                currentIndex, scaleNotes, contourTarget,
                leapRecoveryNeeded, leapDirection,
                isStrongBeat, chordTonePcs,
                options.StepBias, options.LeapSize);

            int motion = nextIndex - currentIndex;
            if (Math.Abs(motion) >= 3)
            {
                leapRecoveryNeeded = true;
                leapDirection = motion > 0 ? 1 : -1;
            }
            else
            {
                leapRecoveryNeeded = false;
            }

            var note = scaleNotes[nextIndex];

            // Dynamic velocity: stronger on strong beats, with phrase shaping
            float baseVelocity = isStrongBeat ? 0.8f : 0.6f;
            // Add contour-based dynamics (louder at peak of phrase)
            float dynamicBoost = contourTarget * 0.15f;
            // Add slight random variation
            float randomVar = (float)(_random.NextDouble() * 0.1f - 0.05f);
            float velocity = Math.Clamp(baseVelocity + dynamicBoost + randomVar, 0.3f, 0.95f);

            events.Add(new NoteEvent(note, currentBeat, duration, velocity));
            currentIndex = nextIndex;
            currentBeat += duration;
        }

        // Last note -> tonic on a strong beat
        if (events.Count > 0 && !events[^1].IsRest)
        {
            var tonicNotes = scaleNotes.Where(n => key.Scale.GetDegreeOf(n) == 1).ToArray();
            if (tonicNotes.Length > 0)
            {
                var closest = tonicNotes.OrderBy(n => Math.Abs(n.MidiNumber - events[^1].Note.MidiNumber)).First();
                events[^1] = events[^1] with { Note = closest, Velocity = 0.85f };
            }
        }

        return new NoteSequence
        {
            Events = events,
            TotalBars = options.Bars,
            BeatsPerBar = options.BeatsPerBar
        };
    }

    /// <summary>
    /// Build a map of bar index -> chord tone pitch classes from the progression.
    /// </summary>
    private static Dictionary<int, HashSet<int>>? BuildChordToneMap(MelodyOptions options)
    {
        if (options.Progression == null) return null;

        var map = new Dictionary<int, HashSet<int>>();
        int bar = 0;
        foreach (var chordEvent in options.Progression.Chords)
        {
            var chordNotes = chordEvent.Chord.GetNotes();
            var pcs = new HashSet<int>(chordNotes.Select(n => n.PitchClass));
            for (int b = 0; b < chordEvent.DurationBars; b++)
            {
                map[bar + b] = pcs;
            }
            bar += chordEvent.DurationBars;
        }
        return map;
    }

    private int PickNextIndex(int current, Note[] scaleNotes, float contourTarget,
        bool leapRecovery, int leapDirection,
        bool isStrongBeat, HashSet<int>? chordTonePcs,
        float stepBias, float leapSize)
    {
        int scaleLength = scaleNotes.Length;

        // Leap recovery
        if (leapRecovery && _random.NextDouble() < 0.75)
        {
            int recoveryStep = leapDirection > 0 ? -1 : 1;
            return Math.Clamp(current + recoveryStep, 0, scaleLength - 1);
        }

        // Prefer chord tones: strongly on strong beats, moderately on weak beats
        if (chordTonePcs != null)
        {
            float chordToneProb = isStrongBeat ? 0.85f : 0.5f;
            if (_random.NextDouble() < chordToneProb)
            {
                var chordIndices = Enumerable.Range(0, scaleLength)
                    .Where(i => chordTonePcs.Contains(scaleNotes[i].PitchClass))
                    .OrderBy(i => Math.Abs(i - current))
                    .ThenBy(i => Math.Abs(i - (int)(contourTarget * (scaleLength - 1))))
                    .ToArray();

                if (chordIndices.Length > 0)
                {
                    // On strong beats pick from top 2 nearest, weak beats top 3
                    int choices = isStrongBeat ? Math.Min(2, chordIndices.Length) : Math.Min(3, chordIndices.Length);
                    return chordIndices[_random.Next(choices)];
                }
            }
        }

        // Stepwise/leap motion
        float smallLeapProb = (1f - stepBias) * 0.65f;
        float roll = (float)_random.NextDouble();
        int maxStep;
        if (roll < stepBias)
            maxStep = 1;
        else if (roll < stepBias + smallLeapProb)
            maxStep = 2;
        else
            maxStep = _random.Next(3, Math.Max(4, (int)leapSize + 1));

        int targetIndex = (int)(contourTarget * (scaleLength - 1));
        int directionBias = targetIndex > current ? 1 : targetIndex < current ? -1 : 0;
        if (_random.NextDouble() < 0.25) directionBias = -directionBias;

        int direction = directionBias != 0 ? directionBias : (_random.Next(2) == 0 ? 1 : -1);
        int step = direction * _random.Next(1, maxStep + 1);
        int next = Math.Clamp(current + step, 0, scaleLength - 1);

        if (next == current && scaleLength > 1)
            next = current > 0 ? current - 1 : current + 1;

        // Avoid dissonant intervals against the chord (minor 2nd = 1 semitone, tritone = 6)
        if (chordTonePcs != null)
        {
            int notePc = scaleNotes[next].PitchClass;
            bool dissonant = chordTonePcs.Any(chordPc =>
            {
                int interval = (notePc - chordPc + 12) % 12;
                return interval is 1 or 6 or 11; // minor 2nd, tritone, major 7th
            });

            if (dissonant)
            {
                // Fall back to nearest chord tone
                var safe = Enumerable.Range(0, scaleLength)
                    .Where(i => chordTonePcs.Contains(scaleNotes[i].PitchClass))
                    .OrderBy(i => Math.Abs(i - current))
                    .FirstOrDefault(current);
                return safe;
            }
        }

        return next;
    }

    private List<float> GenerateRhythm(MelodyOptions options)
    {
        float totalBeats = options.Bars * options.BeatsPerBar;
        var durations = new List<float>();
        float[] pool = GetDurationPool(options.Energy, options.MinNoteDuration, options.MaxNoteDuration);
        float remaining = totalBeats;

        while (remaining > 0.01f)
        {
            var valid = pool.Where(d => d <= remaining + 0.01f).ToArray();
            if (valid.Length == 0) break;

            // Prefer durations that land on beat boundaries
            float currentBeat = totalBeats - remaining;
            var preferred = valid.Where(d =>
            {
                float end = currentBeat + d;
                return end % 1f < 0.01f || end % 0.5f < 0.01f;
            }).ToArray();

            float duration = preferred.Length > 0 && _random.NextDouble() < 0.7
                ? preferred[_random.Next(preferred.Length)]
                : valid[_random.Next(valid.Length)];

            durations.Add(duration);
            remaining -= duration;
        }

        return durations;
    }

    private static float[] GetDurationPool(float energy, float minDuration, float maxDuration)
    {
        float[] allDurations = [0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f];
        var pool = allDurations.Where(d => d >= minDuration && d <= maxDuration).ToList();

        if (energy > 0.7f)
        {
            var shorts = pool.Where(d => d <= 0.5f).ToList();
            pool.AddRange(shorts);
            pool.AddRange(shorts);
        }
        else if (energy < 0.3f)
        {
            var longs = pool.Where(d => d >= 1f).ToList();
            pool.AddRange(longs);
            pool.AddRange(longs);
        }

        return pool.Count > 0 ? pool.ToArray() : [1f];
    }

    private int GetStartingIndex(Note[] scaleNotes, MelodyContour contour)
    {
        int mid = scaleNotes.Length / 2;
        return contour switch
        {
            MelodyContour.Ascending => _random.Next(0, Math.Max(1, mid)),
            MelodyContour.Descending => _random.Next(mid, scaleNotes.Length),
            MelodyContour.Arch => _random.Next(Math.Max(0, mid / 2), mid),
            MelodyContour.Valley => _random.Next(mid, Math.Min(scaleNotes.Length, mid + mid / 2 + 1)),
            MelodyContour.Flat => mid,
            _ => mid
        };
    }

    private static float GetContourTarget(float t, MelodyContour contour)
    {
        return contour switch
        {
            MelodyContour.Ascending => t,
            MelodyContour.Descending => 1f - t,
            MelodyContour.Arch => 1f - 4f * (t - 0.5f) * (t - 0.5f),
            MelodyContour.Valley => 4f * (t - 0.5f) * (t - 0.5f),
            MelodyContour.Flat => 0.5f,
            _ => 0.5f
        };
    }
}
