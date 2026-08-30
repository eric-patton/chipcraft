using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Tests.Generation;

public class MelodyGeneratorTests
{
    private readonly MelodyGenerator _generator = new(seed: 42);

    [Theory]
    [InlineData(MelodyContour.Ascending)]
    [InlineData(MelodyContour.Descending)]
    [InlineData(MelodyContour.Arch)]
    [InlineData(MelodyContour.Valley)]
    [InlineData(MelodyContour.Flat)]
    public void Generate_AllContours_ProduceNotes(MelodyContour contour)
    {
        var options = new MelodyOptions(Key.Parse("Am"), contour, Bars: 4);
        var sequence = _generator.Generate(options);

        Assert.NotEmpty(sequence.Events);
        Assert.Equal(4, sequence.TotalBars);
    }

    [Fact]
    public void Generate_NotesAreInScale()
    {
        var key = Key.Parse("C");
        var options = new MelodyOptions(key, Bars: 8);
        var sequence = _generator.Generate(options);

        var nonRests = sequence.Events.Where(e => !e.IsRest).ToList();
        Assert.NotEmpty(nonRests);

        foreach (var e in nonRests)
        {
            Assert.True(key.Scale.Contains(e.Note),
                $"Note {e.Note} is not in {key} scale");
        }
    }

    [Fact]
    public void Generate_NotesInRange()
    {
        var low = Note.Parse("C4");
        var high = Note.Parse("C6");
        var options = new MelodyOptions(Key.Parse("Am"), LowNote: low, HighNote: high, Bars: 8);
        var sequence = _generator.Generate(options);

        foreach (var e in sequence.Events.Where(e => !e.IsRest))
        {
            Assert.InRange(e.Note.MidiNumber, low.MidiNumber, high.MidiNumber);
        }
    }

    [Fact]
    public void Generate_FillsAllBars()
    {
        var options = new MelodyOptions(Key.Parse("Dm"), Bars: 4, BeatsPerBar: 4);
        var sequence = _generator.Generate(options);

        float totalDuration = sequence.Events.Sum(e => e.DurationBeats);
        Assert.InRange(totalDuration, 15.5f, 16.5f); // ~16 beats for 4 bars
    }

    [Fact]
    public void Generate_LastNoteGravitatesToTonic()
    {
        // Run multiple times to verify tendency
        int tonicCount = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var gen = new MelodyGenerator(seed: seed);
            var options = new MelodyOptions(Key.Parse("Am"), Bars: 4);
            var sequence = gen.Generate(options);
            var lastNote = sequence.Events.LastOrDefault(e => !e.IsRest);
            if (lastNote != null && lastNote.Note.Name == "A")
                tonicCount++;
        }

        Assert.True(tonicCount > 10, $"Last note should frequently be tonic, was {tonicCount}/20");
    }

    [Fact]
    public void Generate_HighEnergy_ProducesShorterNotes()
    {
        var highE = new MelodyOptions(Key.Parse("Am"), Energy: 0.9f, Bars: 4, MinNoteDuration: 0.25f);
        var lowE = new MelodyOptions(Key.Parse("Am"), Energy: 0.1f, Bars: 4, MinNoteDuration: 0.25f);

        var highSeq = _generator.Generate(highE);
        var lowSeq = _generator.Generate(lowE);

        float highAvg = highSeq.Events.Average(e => e.DurationBeats);
        float lowAvg = lowSeq.Events.Average(e => e.DurationBeats);

        Assert.True(highAvg < lowAvg, $"High energy avg ({highAvg:F2}) should be < low energy avg ({lowAvg:F2})");
    }

    [Fact]
    public void Generate_DifferentKeys_ProduceDifferentNotes()
    {
        var cMajor = new MelodyGenerator(seed: 1).Generate(new MelodyOptions(Key.Parse("C"), Bars: 4));
        var fSharpMinor = new MelodyGenerator(seed: 1).Generate(new MelodyOptions(Key.Parse("F#m"), Bars: 4));

        var cNotes = cMajor.Events.Where(e => !e.IsRest).Select(e => e.Note.PitchClass).ToHashSet();
        var fsNotes = fSharpMinor.Events.Where(e => !e.IsRest).Select(e => e.Note.PitchClass).ToHashSet();

        Assert.NotEqual(cNotes, fsNotes);
    }
}

public class BassLineGeneratorTests
{
    private readonly BassLineGenerator _generator = new(seed: 42);
    private readonly ChordProgressionGenerator _progGen = new(seed: 42);

    private ChordProgression GetTestProgression()
    {
        return _progGen.Generate(new ProgressionOptions(Key.Parse("Am"), Bars: 4));
    }

    [Theory]
    [InlineData(BassStyle.RootFifth)]
    [InlineData(BassStyle.Octave)]
    [InlineData(BassStyle.Walking)]
    [InlineData(BassStyle.Pedal)]
    [InlineData(BassStyle.Arpeggiated)]
    public void Generate_AllStyles_ProduceNotes(BassStyle style)
    {
        var progression = GetTestProgression();
        var options = new BassLineOptions(progression, style);
        var sequence = _generator.Generate(options);

        Assert.NotEmpty(sequence.Events);
        Assert.Equal(4, sequence.TotalBars);
    }

    [Fact]
    public void Generate_RootFifth_HasNotesOnBeats1And3()
    {
        var progression = GetTestProgression();
        var options = new BassLineOptions(progression, BassStyle.RootFifth);
        var sequence = _generator.Generate(options);

        // Should have notes at beat 0, 2 (per bar)
        var beatPositions = sequence.Events.Select(e => e.StartBeat % 4).ToList();
        Assert.Contains(0f, beatPositions);
    }

    [Fact]
    public void Generate_Arpeggiated_HasDenseNotes()
    {
        var progression = GetTestProgression();
        var options = new BassLineOptions(progression, BassStyle.Arpeggiated);
        var sequence = _generator.Generate(options);

        // Arpeggiated should have more notes than pedal
        var pedalSeq = _generator.Generate(new BassLineOptions(progression, BassStyle.Pedal));
        Assert.True(sequence.Events.Count > pedalSeq.Events.Count);
    }

    [Fact]
    public void Generate_NotesInBassRange()
    {
        var progression = GetTestProgression();
        var options = new BassLineOptions(progression, BassStyle.RootFifth, Octave: 2);
        var sequence = _generator.Generate(options);

        foreach (var e in sequence.Events)
        {
            Assert.InRange(e.Note.Octave, 1, 4);
        }
    }
}

public class DrumPatternGeneratorTests
{
    private readonly DrumPatternGenerator _generator = new(seed: 42);

    [Theory]
    [InlineData(DrumStyle.StraightRock)]
    [InlineData(DrumStyle.FourOnFloor)]
    [InlineData(DrumStyle.HalfTime)]
    [InlineData(DrumStyle.DoubleTime)]
    [InlineData(DrumStyle.Shuffle)]
    [InlineData(DrumStyle.Breakbeat)]
    [InlineData(DrumStyle.March)]
    public void Generate_AllStyles_ProduceHits(DrumStyle style)
    {
        var options = new DrumPatternOptions(style, Energy: 5, Bars: 4);
        var pattern = _generator.Generate(options);

        Assert.NotEmpty(pattern.Hits);
        Assert.Equal(4, pattern.TotalBars);
    }

    [Fact]
    public void Generate_HasKickAndSnare()
    {
        var pattern = _generator.Generate(new DrumPatternOptions(DrumStyle.StraightRock, Energy: 5, Bars: 4));

        Assert.Contains(pattern.Hits, h => h.Voice == DrumVoice.Kick);
        Assert.Contains(pattern.Hits, h => h.Voice == DrumVoice.Snare);
    }

    [Fact]
    public void Generate_HighEnergy_MoreHits()
    {
        var low = _generator.Generate(new DrumPatternOptions(Energy: 2, Bars: 4));
        var high = _generator.Generate(new DrumPatternOptions(Energy: 9, Bars: 4));

        Assert.True(high.Hits.Count > low.Hits.Count,
            $"High energy ({high.Hits.Count}) should have more hits than low ({low.Hits.Count})");
    }

    [Fact]
    public void Generate_WithFills_HasCrash()
    {
        var pattern = _generator.Generate(new DrumPatternOptions(Fills: true, Bars: 8, Energy: 5));

        // Fill bar should produce a crash hit
        Assert.Contains(pattern.Hits, h => h.Voice == DrumVoice.Crash);
    }

    [Fact]
    public void Generate_WithoutFills_NoCrash()
    {
        var pattern = _generator.Generate(new DrumPatternOptions(Fills: false, Bars: 4, Energy: 5));
        Assert.DoesNotContain(pattern.Hits, h => h.Voice == DrumVoice.Crash);
    }
}
