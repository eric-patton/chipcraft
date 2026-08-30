namespace ChipCraft.Engine.Generation;

public record DrumPatternOptions(
    DrumStyle Style = DrumStyle.StraightRock,
    int Energy = 5,
    int Bars = 4,
    int BeatsPerBar = 4,
    bool Fills = true
);

/// <summary>
/// A single drum hit in a pattern.
/// </summary>
public record DrumHit(DrumVoice Voice, float Beat, float Velocity = 0.8f);

/// <summary>
/// A complete drum pattern: a sequence of hits across multiple bars.
/// </summary>
public class DrumPattern
{
    public List<DrumHit> Hits { get; init; } = [];
    public int TotalBars { get; init; }
    public int BeatsPerBar { get; init; } = 4;
}

/// <summary>
/// Generates drum patterns using template-based generation with energy scaling.
/// Energy 1-3: sparse. 4-6: standard. 7-8: busy. 9-10: intense.
/// Fills are inserted at phrase boundaries (every 4 or 8 bars).
/// </summary>
public class DrumPatternGenerator
{
    private readonly Random _random;

    public DrumPatternGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public DrumPattern Generate(DrumPatternOptions options)
    {
        var hits = new List<DrumHit>();

        for (int bar = 0; bar < options.Bars; bar++)
        {
            float barOffset = bar * options.BeatsPerBar;
            bool isFillBar = options.Fills && (bar + 1) % 4 == 0 && bar > 0;

            if (isFillBar)
            {
                hits.AddRange(GenerateFill(barOffset, options));
            }
            else
            {
                hits.AddRange(GenerateBar(barOffset, options));
            }
        }

        return new DrumPattern
        {
            Hits = hits,
            TotalBars = options.Bars,
            BeatsPerBar = options.BeatsPerBar
        };
    }

    private List<DrumHit> GenerateBar(float barOffset, DrumPatternOptions options)
    {
        return options.Style switch
        {
            DrumStyle.StraightRock => StraightRock(barOffset, options),
            DrumStyle.FourOnFloor => FourOnFloor(barOffset, options),
            DrumStyle.HalfTime => HalfTime(barOffset, options),
            DrumStyle.DoubleTime => DoubleTime(barOffset, options),
            DrumStyle.Shuffle => ShufflePattern(barOffset, options),
            DrumStyle.Breakbeat => Breakbeat(barOffset, options),
            DrumStyle.March => MarchPattern(barOffset, options),
            _ => StraightRock(barOffset, options)
        };
    }

    /// <summary>
    /// Standard rock: kick 1,3 / snare 2,4 / hat on 8ths.
    /// </summary>
    private List<DrumHit> StraightRock(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>();
        int e = o.Energy;

        // Kick: beats 1 and 3
        hits.Add(new DrumHit(DrumVoice.Kick, offset, 0.9f));
        hits.Add(new DrumHit(DrumVoice.Kick, offset + 2f, 0.85f));
        // Extra kicks at high energy
        if (e >= 7 && _random.NextDouble() < 0.5)
            hits.Add(new DrumHit(DrumVoice.Kick, offset + 3.5f, 0.7f));
        if (e >= 9)
            hits.Add(new DrumHit(DrumVoice.Kick, offset + 0.75f, 0.6f));

        // Snare: beats 2 and 4
        hits.Add(new DrumHit(DrumVoice.Snare, offset + 1f, 0.85f));
        hits.Add(new DrumHit(DrumVoice.Snare, offset + 3f, 0.85f));
        // Ghost notes at high energy
        if (e >= 8 && _random.NextDouble() < 0.4)
            hits.Add(new DrumHit(DrumVoice.Snare, offset + 2.5f, 0.4f));

        // Hi-hat
        if (e >= 3)
        {
            float hatInterval = e >= 7 ? 0.25f : 0.5f; // 16ths at high energy
            for (float b = 0; b < o.BeatsPerBar; b += hatInterval)
            {
                float vel = (b % 1f < 0.01f) ? 0.6f : 0.4f; // Accent on beats
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + b, vel));
            }
            // Open hat on "and of 4" for variation
            if (e >= 5 && _random.NextDouble() < 0.3)
                hits.Add(new DrumHit(DrumVoice.HiHatOpen, offset + 3.5f, 0.55f));
        }

        // Reduce hits for low energy
        if (e <= 2)
            hits.RemoveAll(h => h.Voice == DrumVoice.HiHatClosed && h.Beat % 1f > 0.01f);
        if (e <= 1)
            hits.RemoveAll(h => h.Voice == DrumVoice.Kick && MathF.Abs(h.Beat - offset - 2f) < 0.01f);

        return hits;
    }

    /// <summary>
    /// Four-on-the-floor: kick every beat, snare 2,4, hats on off-beats.
    /// </summary>
    private List<DrumHit> FourOnFloor(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>();
        for (int beat = 0; beat < o.BeatsPerBar; beat++)
            hits.Add(new DrumHit(DrumVoice.Kick, offset + beat, 0.85f));
        hits.Add(new DrumHit(DrumVoice.Snare, offset + 1f, 0.8f));
        hits.Add(new DrumHit(DrumVoice.Snare, offset + 3f, 0.8f));

        if (o.Energy >= 3)
        {
            for (float b = 0.5f; b < o.BeatsPerBar; b += 1f)
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + b, 0.5f));
        }
        return hits;
    }

    /// <summary>
    /// Half-time: kick 1, snare 3. Spacious feel.
    /// </summary>
    private List<DrumHit> HalfTime(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>
        {
            new(DrumVoice.Kick, offset, 0.9f),
            new(DrumVoice.Snare, offset + 2f, 0.85f)
        };

        if (o.Energy >= 4)
        {
            for (float b = 0; b < o.BeatsPerBar; b += 0.5f)
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + b, 0.45f));
        }
        return hits;
    }

    /// <summary>
    /// Double-time: kick and snare alternate every half beat.
    /// </summary>
    private List<DrumHit> DoubleTime(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>();
        for (float b = 0; b < o.BeatsPerBar; b += 0.5f)
        {
            int step = (int)(b * 2) % 4;
            if (step is 0 or 2)
                hits.Add(new DrumHit(DrumVoice.Kick, offset + b, 0.85f));
            if (step is 1 or 3)
                hits.Add(new DrumHit(DrumVoice.Snare, offset + b, 0.8f));
        }

        for (float b = 0; b < o.BeatsPerBar; b += 0.25f)
            hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + b, 0.4f));

        return hits;
    }

    /// <summary>
    /// Shuffle: triplet-based hat pattern with swing.
    /// </summary>
    private List<DrumHit> ShufflePattern(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>
        {
            new(DrumVoice.Kick, offset, 0.9f),
            new(DrumVoice.Kick, offset + 2f, 0.85f),
            new(DrumVoice.Snare, offset + 1f, 0.85f),
            new(DrumVoice.Snare, offset + 3f, 0.85f)
        };

        // Triplet hats (swing feel): beat, beat+0.67 (skip the middle triplet)
        if (o.Energy >= 3)
        {
            for (int beat = 0; beat < o.BeatsPerBar; beat++)
            {
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + beat, 0.55f));
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + beat + 0.67f, 0.4f));
            }
        }
        return hits;
    }

    /// <summary>
    /// Breakbeat: syncopated kick pattern.
    /// </summary>
    private List<DrumHit> Breakbeat(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>
        {
            new(DrumVoice.Kick, offset, 0.9f),
            new(DrumVoice.Kick, offset + 1.5f, 0.75f),
            new(DrumVoice.Kick, offset + 2.75f, 0.7f),
            new(DrumVoice.Snare, offset + 1f, 0.85f),
            new(DrumVoice.Snare, offset + 3f, 0.85f)
        };

        if (o.Energy >= 4)
        {
            for (float b = 0; b < o.BeatsPerBar; b += 0.5f)
                hits.Add(new DrumHit(DrumVoice.HiHatClosed, offset + b, 0.45f));
        }
        return hits;
    }

    /// <summary>
    /// March: kick-snare alternating on beats.
    /// </summary>
    private static List<DrumHit> MarchPattern(float offset, DrumPatternOptions o)
    {
        var hits = new List<DrumHit>();
        for (int beat = 0; beat < o.BeatsPerBar; beat++)
        {
            if (beat % 2 == 0)
                hits.Add(new DrumHit(DrumVoice.Kick, offset + beat, 0.85f));
            else
                hits.Add(new DrumHit(DrumVoice.Snare, offset + beat, 0.8f));
        }
        return hits;
    }

    private List<DrumHit> GenerateFill(float barOffset, DrumPatternOptions options)
    {
        var hits = new List<DrumHit>();
        int e = options.Energy;

        // First half: normal pattern
        hits.AddRange(GenerateBar(barOffset, options).Where(h => h.Beat < barOffset + 2f));

        // Second half: fill (snare roll descending in velocity)
        int fillSteps = e >= 7 ? 8 : 4;
        float fillStart = barOffset + 2f;
        float fillStep = 2f / fillSteps;

        for (int i = 0; i < fillSteps; i++)
        {
            float vel = 0.6f + 0.35f * ((float)i / fillSteps); // Crescendo
            var voice = i < fillSteps / 2 ? DrumVoice.Snare : DrumVoice.Tom;
            hits.Add(new DrumHit(voice, fillStart + i * fillStep, vel));
        }

        // Crash on the next downbeat (if this isn't the last bar)
        hits.Add(new DrumHit(DrumVoice.Crash, barOffset + options.BeatsPerBar, 0.9f));

        return hits;
    }
}
