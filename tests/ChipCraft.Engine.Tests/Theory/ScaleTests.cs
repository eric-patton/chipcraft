using ChipCraft.Engine.Models;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Tests.Theory;

public class ScaleTests
{
    [Theory]
    [InlineData(ScaleType.Major, 7)]
    [InlineData(ScaleType.NaturalMinor, 7)]
    [InlineData(ScaleType.HarmonicMinor, 7)]
    [InlineData(ScaleType.MelodicMinor, 7)]
    [InlineData(ScaleType.PentatonicMajor, 5)]
    [InlineData(ScaleType.PentatonicMinor, 5)]
    [InlineData(ScaleType.Blues, 6)]
    [InlineData(ScaleType.Dorian, 7)]
    [InlineData(ScaleType.Mixolydian, 7)]
    [InlineData(ScaleType.Phrygian, 7)]
    [InlineData(ScaleType.Lydian, 7)]
    [InlineData(ScaleType.Locrian, 7)]
    [InlineData(ScaleType.WholeTone, 6)]
    [InlineData(ScaleType.Chromatic, 12)]
    [InlineData(ScaleType.Diminished, 8)]
    public void ScaleDatabase_HasCorrectDegreeCount(ScaleType type, int expectedDegrees)
    {
        Assert.Equal(expectedDegrees, ScaleDatabase.GetDegreeCount(type));
    }

    [Theory]
    [InlineData(ScaleType.Major)]
    [InlineData(ScaleType.NaturalMinor)]
    [InlineData(ScaleType.PentatonicMinor)]
    [InlineData(ScaleType.Blues)]
    [InlineData(ScaleType.Dorian)]
    [InlineData(ScaleType.Chromatic)]
    public void ScaleDatabase_IntervalsStartAtZero(ScaleType type)
    {
        var intervals = ScaleDatabase.GetIntervals(type);
        Assert.Equal(0, intervals[0]);
    }

    [Theory]
    [InlineData(ScaleType.Major)]
    [InlineData(ScaleType.NaturalMinor)]
    [InlineData(ScaleType.PentatonicMinor)]
    public void ScaleDatabase_IntervalsAreAscending(ScaleType type)
    {
        var intervals = ScaleDatabase.GetIntervals(type);
        for (int i = 1; i < intervals.Length; i++)
            Assert.True(intervals[i] > intervals[i - 1]);
    }

    [Fact]
    public void CMajor_HasCorrectNotes()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        var names = scale.GetNoteNames();
        Assert.Equal(["C", "D", "E", "F", "G", "A", "B"], names);
    }

    [Fact]
    public void AMinor_HasCorrectNotes()
    {
        var scale = new Scale(Note.Parse("A4"), ScaleType.NaturalMinor);
        var names = scale.GetNoteNames();
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G"], names);
    }

    [Fact]
    public void GetNotesInRange_ReturnsCorrectCount()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        var notes = scale.GetNotesInRange(Note.Parse("C4"), Note.Parse("C5"));

        // C4 D4 E4 F4 G4 A4 B4 C5 = 8 notes
        Assert.Equal(8, notes.Length);
    }

    [Fact]
    public void GetDegree_ReturnsCorrectNote()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);

        Assert.Equal("C", scale.GetDegree(1, 4).Name);
        Assert.Equal("E", scale.GetDegree(3, 4).Name);
        Assert.Equal("G", scale.GetDegree(5, 4).Name);
    }

    [Fact]
    public void GetDegree_WrapsOctave()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        var degree8 = scale.GetDegree(8, 4); // Should be C one octave up
        Assert.Equal("C", degree8.Name);
        Assert.Equal(5, degree8.Octave);
    }

    [Fact]
    public void Contains_TrueForScaleNotes()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        Assert.True(scale.Contains(Note.Parse("C4")));
        Assert.True(scale.Contains(Note.Parse("E5")));
        Assert.True(scale.Contains(Note.Parse("G3")));
    }

    [Fact]
    public void Contains_FalseForNonScaleNotes()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        Assert.False(scale.Contains(Note.Parse("C#4")));
        Assert.False(scale.Contains(Note.Parse("Eb4")));
    }

    [Fact]
    public void GetDegreeOf_ReturnsCorrectDegree()
    {
        var scale = new Scale(Note.Parse("C4"), ScaleType.Major);
        Assert.Equal(1, scale.GetDegreeOf(Note.Parse("C4")));
        Assert.Equal(3, scale.GetDegreeOf(Note.Parse("E5")));
        Assert.Equal(5, scale.GetDegreeOf(Note.Parse("G3")));
        Assert.Null(scale.GetDegreeOf(Note.Parse("C#4")));
    }

    [Fact]
    public void Scale_StringConstructor_Works()
    {
        var scale = new Scale("D", ScaleType.Dorian);
        var names = scale.GetNoteNames();
        Assert.Equal("D", names[0]);
    }

    [Theory]
    [InlineData(ScaleType.NaturalMinor, true)]
    [InlineData(ScaleType.Major, false)]
    [InlineData(ScaleType.Blues, true)]
    [InlineData(ScaleType.Dorian, true)]
    [InlineData(ScaleType.Mixolydian, false)]
    public void IsMinor_CorrectForScaleType(ScaleType type, bool expectedMinor)
    {
        Assert.Equal(expectedMinor, ScaleDatabase.IsMinor(type));
    }
}
