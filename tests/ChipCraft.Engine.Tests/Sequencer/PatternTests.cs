using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Engine.Tests.Sequencer;

public class PatternTests
{
    [Fact]
    public void Pattern_DefaultsToEmptyCells()
    {
        var pattern = new Pattern(16, 4);
        Assert.True(pattern.GetCell(0, 0).IsEmpty);
        Assert.True(pattern.GetCell(15, 3).IsEmpty);
    }

    [Fact]
    public void Pattern_SetAndGetCell()
    {
        var pattern = new Pattern(16, 4);
        var cell = new PatternCell(Note.Parse("C4"), "lead", 12);

        pattern.SetCell(0, 0, cell);
        var retrieved = pattern.GetCell(0, 0);

        Assert.Equal(Note.Parse("C4"), retrieved.Note);
        Assert.Equal("lead", retrieved.InstrumentId);
        Assert.Equal((byte)12, retrieved.Volume);
    }

    [Fact]
    public void Pattern_ClearCell()
    {
        var pattern = new Pattern(16, 4);
        pattern.SetCell(5, 2, new PatternCell(Note.Parse("E4")));
        pattern.ClearCell(5, 2);
        Assert.True(pattern.GetCell(5, 2).IsEmpty);
    }

    [Fact]
    public void Pattern_OutOfRange_Throws()
    {
        var pattern = new Pattern(16, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.GetCell(16, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.GetCell(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.GetCell(-1, 0));
    }

    [Fact]
    public void Song_SetProgramAndPattern()
    {
        var song = new Song();
        song.InitializeChannels(4);

        song.SetChannelProgram(0, ChipCraft.Engine.Midi.GeneralMidi.GetProgram(0));

        var pattern = new Pattern(16, 4);
        int idx = song.AddPattern(pattern);
        song.AddToOrder(idx);

        Assert.Single(song.ChannelPrograms);
        Assert.Single(song.Patterns);
        Assert.Single(song.OrderList.Entries);
        Assert.Equal(16, song.TotalRows);
    }

    [Fact]
    public void Song_TotalDuration_CalculatesCorrectly()
    {
        var song = new Song { Tempo = 120, RowsPerBeat = 4 };
        song.InitializeChannels(4);
        song.AddPattern(new Pattern(64, 4)); // 64 rows = 16 beats = 4 bars
        song.AddToOrder(0);

        // At 120 BPM, 16 beats = 8 seconds
        Assert.InRange(song.TotalDurationSeconds, 7.9, 8.1);
    }

    [Fact]
    public void Pattern_ToNoteSequence_ReconstructsDurations()
    {
        var pattern = new Pattern(16, 1);
        pattern.SetCell(0, 0, new PatternCell(Note.Parse("C4"), null, 12));
        pattern.SetCell(4, 0, new PatternCell(Note.Cut));
        pattern.SetCell(8, 0, new PatternCell(Note.Parse("E4"), null, 10));
        pattern.SetCell(12, 0, new PatternCell(Note.Cut));

        var sequence = pattern.ToNoteSequence(0);

        Assert.Equal(2, sequence.Events.Count);
        Assert.Equal(Note.Parse("C4"), sequence.Events[0].Note);
        Assert.Equal(1f, sequence.Events[0].DurationBeats);
        Assert.Equal(Note.Parse("E4"), sequence.Events[1].Note);
        Assert.Equal(1f, sequence.Events[1].DurationBeats);
    }
}
