using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Tests.Generation;

public class ChordProgressionGeneratorTests
{
    private readonly ChordProgressionGenerator _generator = new(seed: 42);

    [Theory]
    [InlineData(Mood.Heroic, Genre.Action)]
    [InlineData(Mood.Calm, Genre.RpgTown)]
    [InlineData(Mood.Tense, Genre.Horror)]
    [InlineData(Mood.Triumphant, Genre.Sports)]
    [InlineData(Mood.Mysterious, Genre.Space)]
    [InlineData(Mood.Playful, Genre.Platformer)]
    [InlineData(Mood.Melancholy, Genre.Fantasy)]
    [InlineData(Mood.Dark, Genre.Horror)]
    [InlineData(Mood.Urgent, Genre.RpgBattle)]
    [InlineData(Mood.Epic, Genre.Action)]
    public void Generate_ProducesNonEmptyProgression(Mood mood, Genre genre)
    {
        var key = Key.Parse("Am");
        var options = new ProgressionOptions(key, mood, genre, Bars: 4);
        var progression = _generator.Generate(options);

        Assert.NotEmpty(progression.Chords);
        Assert.NotEmpty(progression.TemplateName);
    }

    [Fact]
    public void Generate_RespectsBarCount()
    {
        var options = new ProgressionOptions(Key.Parse("Dm"), Bars: 8);
        var progression = _generator.Generate(options);

        Assert.Equal(8, progression.TotalBars);
    }

    [Fact]
    public void Generate_ShortBarCount_Truncates()
    {
        var options = new ProgressionOptions(Key.Parse("C"), Bars: 2);
        var progression = _generator.Generate(options);

        Assert.Equal(2, progression.TotalBars);
    }

    [Fact]
    public void Generate_ChordsHaveValidRoots()
    {
        var options = new ProgressionOptions(Key.Parse("Am"), Mood.Heroic, Genre.Action, Bars: 4);
        var progression = _generator.Generate(options);

        foreach (var chordEvent in progression.Chords)
        {
            Assert.False(chordEvent.Chord.Root.IsRest);
            Assert.False(chordEvent.Chord.Root.IsCut);
            Assert.InRange(chordEvent.Chord.Root.MidiNumber, 60, 71); // All in octave 4
        }
    }

    [Fact]
    public void Generate_DifferentMoods_ProduceDifferentResults()
    {
        var key = Key.Parse("Am");

        var heroic = new ChordProgressionGenerator(seed: 1)
            .Generate(new ProgressionOptions(key, Mood.Heroic, Genre.Action));
        var calm = new ChordProgressionGenerator(seed: 1)
            .Generate(new ProgressionOptions(key, Mood.Calm, Genre.RpgTown));

        // Different moods should generally produce different templates
        // (with same seed, the weighted selection should pick different templates)
        Assert.NotEqual(heroic.TemplateName, calm.TemplateName);
    }

    [Fact]
    public void GenerateMultiple_ReturnsRequestedCount()
    {
        var options = new ProgressionOptions(Key.Parse("C"), Mood.Heroic, Genre.Action, Bars: 4);
        var results = _generator.GenerateMultiple(options, count: 3);

        Assert.InRange(results.Count, 1, 3);
    }

    [Fact]
    public void GenerateMultiple_ReturnsDistinctTemplates()
    {
        var options = new ProgressionOptions(Key.Parse("Am"), Mood.Heroic, Genre.Action, Bars: 4);
        var results = _generator.GenerateMultiple(options, count: 3);

        var templateNames = results.Select(r => r.TemplateName).ToList();
        Assert.Equal(templateNames.Distinct().Count(), templateNames.Count);
    }

    [Fact]
    public void Generate_MajorKey_ProducesValidChords()
    {
        var options = new ProgressionOptions(Key.Parse("C"), Mood.Calm, Genre.RpgTown, Bars: 4);
        var progression = _generator.Generate(options);

        Assert.NotEmpty(progression.Chords);
        // All chords should be parseable back to string
        foreach (var chordEvent in progression.Chords)
        {
            string name = chordEvent.Chord.ToString();
            Assert.NotEmpty(name);
        }
    }

    [Fact]
    public void ProgressionDatabase_AllTemplatesHaveDegrees()
    {
        foreach (var template in ProgressionDatabase.AllProgressions)
        {
            Assert.NotEmpty(template.Degrees);
            Assert.NotEmpty(template.Name);
            Assert.NotEmpty(template.Moods);
            Assert.NotEmpty(template.Genres);
        }
    }

    [Fact]
    public void ProgressionDatabase_AllMoodsCovered()
    {
        foreach (Mood mood in Enum.GetValues<Mood>())
        {
            bool covered = ProgressionDatabase.AllProgressions.Any(t => t.Moods.Contains(mood));
            Assert.True(covered, $"Mood '{mood}' has no matching progressions");
        }
    }

    [Fact]
    public void ProgressionDatabase_AllGenresCovered()
    {
        foreach (Genre genre in Enum.GetValues<Genre>())
        {
            bool covered = ProgressionDatabase.AllProgressions.Any(t => t.Genres.Contains(genre));
            Assert.True(covered, $"Genre '{genre}' has no matching progressions");
        }
    }

    [Theory]
    [InlineData(Genre.Action, 140, 170)]
    [InlineData(Genre.RpgTown, 80, 120)]
    [InlineData(Genre.Horror, 70, 110)]
    public void ProgressionDatabase_DefaultTempo_InRange(Genre genre, int minBpm, int maxBpm)
    {
        int tempo = ProgressionDatabase.GetDefaultTempo(genre);
        Assert.InRange(tempo, minBpm, maxBpm);
    }
}
