using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Persistence;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Tests.Composition;

public class SongComposerTests
{
    [Fact]
    public void ResolveSpec_AppliesDefaultsAndRoundsBars()
    {
        var composer = new SongComposer();

        var spec = composer.ResolveSpec(
            "Battle Cue",
            "",
            Genre.Action,
            Mood.Heroic,
            bars: 15,
            loop: null,
            key: null,
            scaleType: null,
            tempo: null,
            palette: null,
            seed: 42);

        Assert.Equal(16, spec.Bars);
        Assert.True(spec.Loop);
        Assert.Equal("Am", spec.KeyName);
        Assert.Equal(ScaleType.NaturalMinor, spec.ScaleType);
        Assert.Equal(150, spec.Tempo);
        Assert.Equal(PaletteProfileLibrary.DefaultPaletteName, spec.Palette);
        Assert.Equal(42, spec.Seed);
        Assert.Equal("loop-variation", spec.FormHint);
    }

    [Fact]
    public void ResolveSpec_InfersPaletteAndFormHintsFromPrompt()
    {
        var composer = new SongComposer();

        var spec = composer.ResolveSpec(
            "Menu Cue",
            "ambient orchestral mini-song for a menu",
            Genre.Fantasy,
            Mood.Calm,
            bars: 24,
            loop: true);

        Assert.Equal("cinematic", spec.Palette);
        Assert.Equal("mini-song", spec.FormHint);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void Compose_ProducesLoopedCueWithCoreRoles(int bars)
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec(
            $"Cue {bars}",
            "driving game cue",
            Genre.Action,
            Mood.Heroic,
            bars: bars,
            seed: 1234);

        var result = composer.Compose(spec);

        Assert.Equal(bars * 16, result.Song.TotalRows);
        Assert.Equal(0, result.Song.OrderList.LoopStartIndex);
        Assert.Equal(6, result.Song.ChannelCount);
        Assert.Contains(result.Metadata.ChannelAssignments, assignment => assignment.Role == ChannelRole.Lead);
        Assert.Contains(result.Metadata.ChannelAssignments, assignment => assignment.Role == ChannelRole.Bass);
        Assert.Contains(result.Metadata.ChannelAssignments, assignment => assignment.Role == ChannelRole.Drums);
        Assert.Contains(result.Metadata.ChannelAssignments, assignment => assignment.Role == ChannelRole.Harmony);
        Assert.NotNull(result.Metadata.Analysis);
        Assert.NotEmpty(result.Song.Patterns);
    }

    [Fact]
    public void Compose_WithSeed_IsDeterministic()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec(
            "Deterministic Cue",
            "retro battle theme",
            Genre.RpgBattle,
            Mood.Epic,
            bars: 16,
            seed: 777);

        var first = composer.Compose(spec);
        var second = composer.Compose(spec);

        Assert.Equal(BuildSongSignature(first), BuildSongSignature(second));
        Assert.Equal(first.Metadata.ArrangementPlan?.Form, second.Metadata.ArrangementPlan?.Form);
    }

    [Fact]
    public void Compose_PopulatesCandidateRankings()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Candidate Cue", "", Genre.Action, Mood.Heroic, bars: 16, seed: 314);

        var result = composer.Compose(spec);

        Assert.NotEmpty(result.Metadata.CandidateList);
        Assert.True(result.Metadata.CandidateList.Count >= 3);
        Assert.NotNull(result.Metadata.SelectedCandidateIndex);
        Assert.All(result.Metadata.CandidateList, candidate => Assert.InRange(candidate.OverallScore, 0, 1));
    }

    [Fact]
    public void Compose_MiniSongForm_UsesAdvancedSectionLabels()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec(
            "Mini Song Cue",
            "full cue with lift",
            Genre.Fantasy,
            Mood.Heroic,
            bars: 16,
            seed: 121,
            form: "mini-song");

        var result = composer.Compose(spec);
        var labels = result.Metadata.ArrangementPlan!.Sections.Select(section => section.Label).ToArray();

        Assert.Equal(new[] { "Intro", "A", "B", "Hook" }, labels);
    }

    [Fact]
    public void Compose_LinearArc_AddsTempoShape()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec(
            "Linear Cue",
            "story arc",
            Genre.Fantasy,
            Mood.Mysterious,
            bars: 16,
            loop: false,
            seed: 202,
            form: "linear-arc");

        var result = composer.Compose(spec);
        var labels = result.Metadata.ArrangementPlan!.Sections.Select(section => section.Label).ToArray();

        Assert.Contains("Intro", labels);
        Assert.Contains(result.Song.OrderList.Entries, entry => entry.TempoOverride.HasValue);
    }

    [Fact]
    public void Analyze_FlagsRepeatedPatterns()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Loop Test", "", Genre.Action, Mood.Heroic, bars: 16, seed: 99);
        var result = composer.Compose(spec);

        var firstPattern = result.Song.Patterns[0];
        for (int i = 1; i < result.Song.Patterns.Count; i++)
            CopyPattern(firstPattern, result.Song.Patterns[i]);

        var analysis = new SongAnalyzer().Analyze(result.Song, result.Metadata);
        Assert.True(analysis.PhraseVariation.Score < 0.7, $"Expected low phrase variation, got {analysis.PhraseVariation.Score:F2}");
    }

    [Fact]
    public void Analyze_ReturnsExpandedMetrics()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Analyzer Cue", "driving but memorable", Genre.Action, Mood.Heroic, bars: 16, seed: 812);
        var result = composer.Compose(spec);

        var analysis = new SongAnalyzer().Analyze(result.Song, result.Metadata);

        Assert.InRange(analysis.MelodyMemorability.Score, 0, 1);
        Assert.InRange(analysis.SectionContrast.Score, 0, 1);
        Assert.InRange(analysis.CadenceStrength.Score, 0, 1);
        Assert.InRange(analysis.ChannelCrowding.Score, 0, 1);
    }

    [Fact]
    public void Revise_BusierDrums_IncreasesDrumDensity()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Revision Cue", "", Genre.Action, Mood.Heroic, bars: 16, seed: 901);
        var result = composer.Compose(spec);
        int drumChannel = result.Metadata.ChannelAssignments.First(a => a.Role == ChannelRole.Drums).Channel;
        int before = CountSongStarts(result.Song, drumChannel);

        var revised = new SongRevisionEngine().Revise(result.Song, result.Metadata, "make the drums busier", seed: 901);
        int after = CountSongStarts(revised.Song, drumChannel);

        Assert.True(after >= before, $"Expected revised drum density >= original ({before}), got {after}");
    }

    [Fact]
    public void Revise_TargetedSection_RewritesOnlySpecifiedSection()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Section Revision Cue", "", Genre.Action, Mood.Heroic, bars: 16, seed: 640);
        var result = composer.Compose(spec);

        string beforeA = BuildPatternSignature(result.Song.Patterns[0], 0);
        string beforeB = BuildPatternSignature(result.Song.Patterns[2], 0);

        var revised = new SongRevisionEngine().Revise(result.Song, result.Metadata, "rewrite B and make it bigger", seed: 640, sectionLabel: "B");

        string afterA = BuildPatternSignature(revised.Song.Patterns[0], 0);
        string afterB = BuildPatternSignature(revised.Song.Patterns[2], 0);

        Assert.Equal(beforeA, afterA);
        Assert.NotEqual(beforeB, afterB);
    }

    [Fact]
    public void SongSerializer_RoundTrip_PreservesProgramsMixAndLoop()
    {
        var composer = new SongComposer();
        var spec = composer.ResolveSpec("Persisted Cue", "", Genre.RpgTown, Mood.Calm, bars: 8, loop: false, seed: 55, form: "linear-arc");
        var result = composer.Compose(spec);

        string json = SongSerializer.Serialize(result.Song);
        var roundTrip = SongSerializer.Deserialize(json);

        Assert.Equal(result.Song.Tempo, roundTrip.Tempo);
        Assert.Equal(result.Song.ChannelCount, roundTrip.ChannelCount);
        Assert.Equal(result.Song.OrderList.LoopStartIndex, roundTrip.OrderList.LoopStartIndex);
        Assert.Equal(result.Song.ChannelPrograms.Count, roundTrip.ChannelPrograms.Count);
        Assert.Equal(result.Song.ChannelVolumes, roundTrip.ChannelVolumes);
        Assert.Equal(result.Song.ChannelPans, roundTrip.ChannelPans);
        Assert.Equal(result.Song.Patterns.Count, roundTrip.Patterns.Count);
        Assert.Equal(result.Song.TotalRows, roundTrip.TotalRows);
        Assert.Equal(result.Song.OrderList.Entries.Select(entry => entry.TempoOverride), roundTrip.OrderList.Entries.Select(entry => entry.TempoOverride));
    }

    [Fact]
    public void StemLayoutLibrary_ResolvesDefaultAdaptiveGroups()
    {
        var palette = PaletteProfileLibrary.Resolve("cinematic", Mood.Heroic, Genre.Fantasy);
        var stems = StemLayoutLibrary.Resolve(palette.Assignments);

        Assert.Contains(stems, stem => stem.Name == "lead");
        Assert.Contains(stems, stem => stem.Name == "rhythm");
        Assert.Contains(stems, stem => stem.Name == "harmony");
        Assert.Equal(new[] { 1, 2 }, StemLayoutLibrary.ResolveChannels(stems.First(stem => stem.Name == "rhythm"), palette.Assignments));
    }

    private static string BuildSongSignature(SongCompositionResult result)
    {
        var parts = new List<string>
        {
            result.Metadata.Spec?.KeyName ?? "",
            result.Song.Tempo.ToString(),
            result.Metadata.ArrangementPlan?.Form ?? ""
        };

        foreach (var pattern in result.Song.Patterns)
        {
            for (int channel = 0; channel < pattern.ChannelCount; channel++)
            {
                string signature = string.Join("|", pattern.ToNoteSequence(channel).Events.Select(evt =>
                    $"{evt.StartBeat:0.##}:{evt.DurationBeats:0.##}:{evt.Note.MidiNumber}"));
                parts.Add(signature);
            }
        }

        return string.Join("\n", parts);
    }

    private static void CopyPattern(ChipCraft.Engine.Sequencer.Pattern source, ChipCraft.Engine.Sequencer.Pattern target)
    {
        for (int row = 0; row < source.RowCount; row++)
            for (int channel = 0; channel < source.ChannelCount; channel++)
                target.SetCell(row, channel, source.GetCell(row, channel));
    }

    private static int CountSongStarts(ChipCraft.Engine.Sequencer.Song song, int channel)
    {
        int count = 0;
        foreach (var entry in song.OrderList.Entries)
        {
            var pattern = song.Patterns[entry.PatternIndex];
            for (int row = 0; row < pattern.RowCount; row++)
            {
                var cell = pattern.GetCell(row, channel);
                if (cell.Note.HasValue && !cell.Note.Value.IsRest && !cell.Note.Value.IsCut)
                    count++;
            }
        }

        return count;
    }

    private static string BuildPatternSignature(ChipCraft.Engine.Sequencer.Pattern pattern, int channel)
    {
        return string.Join("|", pattern.ToNoteSequence(channel).Events.Select(evt =>
            $"{evt.StartBeat:0.##}:{evt.DurationBeats:0.##}:{evt.Note.MidiNumber}"));
    }
}
