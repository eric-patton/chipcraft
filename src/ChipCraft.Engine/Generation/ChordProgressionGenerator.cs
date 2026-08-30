using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Generation;

/// <summary>
/// A realized chord progression: a sequence of chords with durations.
/// </summary>
public class ChordProgression
{
    public Key Key { get; init; } = Key.Parse("Am");
    public List<ChordEvent> Chords { get; init; } = [];
    public string TemplateName { get; init; } = "";

    public int TotalBars => Chords.Sum(c => c.DurationBars);

    public override string ToString()
    {
        var chordNames = Chords.Select(c => c.Chord.ToString());
        return $"{Key}: {string.Join(" | ", chordNames)}";
    }
}

public record ChordEvent(Chord Chord, int DurationBars = 1);

/// <summary>
/// Options for chord progression generation.
/// </summary>
public record ProgressionOptions(
    Key Key,
    Mood Mood = Mood.Heroic,
    Genre Genre = Genre.Action,
    int Bars = 4
);

/// <summary>
/// Generates chord progressions from mood, genre, and key parameters.
/// Selects from the ProgressionDatabase, scores templates, and realizes
/// roman numeral degrees into actual chords in the requested key.
/// </summary>
public class ChordProgressionGenerator
{
    private readonly Random _random;

    public ChordProgressionGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Generate a chord progression matching the given options.
    /// </summary>
    public ChordProgression Generate(ProgressionOptions options)
    {
        var template = SelectTemplate(options);
        var chords = RealizeProgression(template, options.Key);
        var expanded = ExpandToBarCount(chords, template, options.Bars);

        return new ChordProgression
        {
            Key = options.Key,
            Chords = expanded,
            TemplateName = template.Name
        };
    }

    /// <summary>
    /// Generate multiple progression suggestions, ranked by fitness.
    /// </summary>
    public List<ChordProgression> GenerateMultiple(ProgressionOptions options, int count = 3)
    {
        var scored = ProgressionDatabase.AllProgressions
            .Select(t => (Template: t, Score: ProgressionDatabase.ScoreProgression(t, options.Mood, options.Genre)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(count * 2) // Take extra to allow variety
            .ToList();

        if (scored.Count == 0)
        {
            // Fallback: use the first few progressions
            scored = ProgressionDatabase.AllProgressions
                .Take(count)
                .Select(t => (Template: t, Score: 1.0))
                .ToList();
        }

        var results = new List<ChordProgression>();
        var used = new HashSet<string>();

        foreach (var (template, _) in scored)
        {
            if (results.Count >= count) break;
            if (used.Contains(template.Name)) continue;
            used.Add(template.Name);

            var chords = RealizeProgression(template, options.Key);
            var expanded = ExpandToBarCount(chords, template, options.Bars);

            results.Add(new ChordProgression
            {
                Key = options.Key,
                Chords = expanded,
                TemplateName = template.Name
            });
        }

        return results;
    }

    private ProgressionDatabase.ProgressionTemplate SelectTemplate(ProgressionOptions options)
    {
        var scored = ProgressionDatabase.AllProgressions
            .Select(t => (Template: t, Score: ProgressionDatabase.ScoreProgression(t, options.Mood, options.Genre)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();

        if (scored.Count == 0)
            return ProgressionDatabase.AllProgressions[_random.Next(ProgressionDatabase.AllProgressions.Count)];

        // Weighted random from top candidates
        double totalScore = scored.Sum(x => x.Score);
        double roll = _random.NextDouble() * totalScore;
        double cumulative = 0;

        foreach (var (template, score) in scored)
        {
            cumulative += score;
            if (roll <= cumulative)
                return template;
        }

        return scored[0].Template;
    }

    private static List<ChordEvent> RealizeProgression(
        ProgressionDatabase.ProgressionTemplate template, Key key)
    {
        var chords = new List<ChordEvent>();
        int rootPc = key.Root.PitchClass;
        bool isMinor = key.IsMinor || template.PreferMinor;

        // Build the working scale intervals
        var scaleIntervals = ScaleDatabase.GetIntervals(key.ScaleType);

        foreach (var degree in template.Degrees)
        {
            int absDeg = degree.AbsDegree;
            int semitones;

            if (degree.IsFlat)
            {
                // Flat degree: take the scale degree and lower by 1 semitone
                // e.g., bVII in Am = G (7th degree of A natural minor is already G, which is 10 semitones)
                // For major keys, bVII = 10 semitones (one below the natural 7th at 11)
                semitones = GetDegreeIntervalWithFlat(absDeg, scaleIntervals, isMinor);
            }
            else
            {
                semitones = GetDegreeInterval(absDeg, scaleIntervals);
            }

            int chordRootPc = (rootPc + semitones) % 12;
            var chordRoot = Note.FromMidi(60 + chordRootPc);

            // Determine chord quality
            ChordQuality quality;
            if (degree.QualityOverride == 1)
                quality = ChordQuality.Major;
            else if (degree.QualityOverride == -1)
                quality = ChordQuality.Minor;
            else
                quality = InferDiatonicQuality(absDeg, isMinor);

            chords.Add(new ChordEvent(new Chord(chordRoot, quality)));
        }

        return chords;
    }

    private static int GetDegreeInterval(int degree, int[] scaleIntervals)
    {
        if (degree < 1 || degree > scaleIntervals.Length)
        {
            // Default: use chromatic mapping for out-of-range degrees
            return (degree - 1) * 2; // rough approximation
        }
        return scaleIntervals[degree - 1];
    }

    private static int GetDegreeIntervalWithFlat(int degree, int[] scaleIntervals, bool isMinor)
    {
        // For flat degrees, we lower the major scale degree by 1 semitone
        // bVII = 10 (whole step below octave), bVI = 8, bIII = 3, bII = 1
        return degree switch
        {
            2 => 1,   // bII
            3 => 3,   // bIII
            5 => 6,   // bV (tritone)
            6 => 8,   // bVI
            7 => 10,  // bVII
            _ => GetDegreeInterval(degree, scaleIntervals)
        };
    }

    private static ChordQuality InferDiatonicQuality(int degree, bool isMinor)
    {
        if (isMinor)
        {
            return degree switch
            {
                1 => ChordQuality.Minor,
                2 => ChordQuality.Diminished,
                3 => ChordQuality.Major,
                4 => ChordQuality.Minor,
                5 => ChordQuality.Minor, // Natural minor has minor v; harmonic has major V
                6 => ChordQuality.Major,
                7 => ChordQuality.Major,
                _ => ChordQuality.Major
            };
        }
        else
        {
            return degree switch
            {
                1 => ChordQuality.Major,
                2 => ChordQuality.Minor,
                3 => ChordQuality.Minor,
                4 => ChordQuality.Major,
                5 => ChordQuality.Major,
                6 => ChordQuality.Minor,
                7 => ChordQuality.Diminished,
                _ => ChordQuality.Major
            };
        }
    }

    private List<ChordEvent> ExpandToBarCount(
        List<ChordEvent> chords, ProgressionDatabase.ProgressionTemplate template, int targetBars)
    {
        if (chords.Count == 0) return chords;

        int patternBars = chords.Count; // Each chord = 1 bar in the template
        if (patternBars >= targetBars)
            return chords.Take(targetBars).ToList();

        // Repeat the pattern to fill the target bar count
        var expanded = new List<ChordEvent>();
        int barsRemaining = targetBars;

        while (barsRemaining > 0)
        {
            int barsToAdd = Math.Min(patternBars, barsRemaining);
            for (int i = 0; i < barsToAdd; i++)
            {
                expanded.Add(chords[i]);
            }
            barsRemaining -= barsToAdd;
        }

        return expanded;
    }
}
