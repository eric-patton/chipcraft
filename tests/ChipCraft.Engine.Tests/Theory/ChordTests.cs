using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Tests.Theory;

public class ChordTests
{
    [Theory]
    [InlineData("C", ChordQuality.Major, "C")]
    [InlineData("Am", ChordQuality.Minor, "A")]
    [InlineData("F#dim", ChordQuality.Diminished, "F#")]
    [InlineData("G7", ChordQuality.Dom7, "G")]
    [InlineData("Bbmaj7", ChordQuality.Maj7, "A#")]  // Bb = A#
    [InlineData("Dm", ChordQuality.Minor, "D")]
    [InlineData("E", ChordQuality.Major, "E")]
    [InlineData("Dsus4", ChordQuality.Sus4, "D")]
    [InlineData("Csus2", ChordQuality.Sus2, "C")]
    [InlineData("A5", ChordQuality.Power, "A")]
    public void Parse_CorrectRootAndQuality(string symbol, ChordQuality expectedQuality, string expectedRoot)
    {
        var chord = Chord.Parse(symbol);
        Assert.Equal(expectedQuality, chord.Quality);
        Assert.Equal(expectedRoot, chord.Root.Name);
    }

    [Fact]
    public void CMajor_HasCorrectNotes()
    {
        var chord = Chord.Parse("C");
        var names = chord.GetNoteNames();
        Assert.Equal(["C", "E", "G"], names);
    }

    [Fact]
    public void AMinor_HasCorrectNotes()
    {
        var chord = Chord.Parse("Am");
        var names = chord.GetNoteNames();
        Assert.Equal(["A", "C", "E"], names);
    }

    [Fact]
    public void G7_HasFourNotes()
    {
        var chord = Chord.Parse("G7");
        var notes = chord.GetNotes();
        Assert.Equal(4, notes.Length);
    }

    [Fact]
    public void PowerChord_HasTwoNotes()
    {
        var chord = Chord.Parse("A5");
        var notes = chord.GetNotes();
        Assert.Equal(2, notes.Length);
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        string[] symbols = ["C", "Am", "F#dim", "G7", "Dsus4"];
        foreach (var symbol in symbols)
        {
            var chord = Chord.Parse(symbol);
            var reparsed = Chord.Parse(chord.ToString());
            Assert.Equal(chord.Quality, reparsed.Quality);
            Assert.Equal(chord.Root.PitchClass, reparsed.Root.PitchClass);
        }
    }

    [Theory]
    [InlineData(ChordQuality.Major, 3)]
    [InlineData(ChordQuality.Minor, 3)]
    [InlineData(ChordQuality.Dom7, 4)]
    [InlineData(ChordQuality.Maj7, 4)]
    [InlineData(ChordQuality.Power, 2)]
    [InlineData(ChordQuality.Dom9, 5)]
    public void ChordDatabase_CorrectNoteCount(ChordQuality quality, int expectedNotes)
    {
        Assert.Equal(expectedNotes, ChordDatabase.GetNoteCount(quality));
    }
}

public class KeyTests
{
    [Theory]
    [InlineData("C", false)]
    [InlineData("Am", true)]
    [InlineData("Dm", true)]
    [InlineData("G", false)]
    [InlineData("F#m", true)]
    public void Parse_CorrectMinorDetection(string keyStr, bool expectedMinor)
    {
        var key = Key.Parse(keyStr);
        Assert.Equal(expectedMinor, key.IsMinor);
    }

    [Fact]
    public void CMajor_RelativeMinor_IsAMinor()
    {
        var key = Key.Parse("C");
        var relative = key.GetRelativeKey();
        Assert.Equal("A", relative.Root.Name);
        Assert.True(relative.IsMinor);
    }

    [Fact]
    public void AMinor_RelativeMajor_IsCMajor()
    {
        var key = Key.Parse("Am");
        var relative = key.GetRelativeKey();
        Assert.Equal("C", relative.Root.Name);
        Assert.False(relative.IsMinor);
    }

    [Fact]
    public void ParallelKey_SwapsMajorMinor()
    {
        var major = Key.Parse("C");
        var parallel = major.GetParallelKey();
        Assert.Equal("C", parallel.Root.Name);
        Assert.True(parallel.IsMinor);
    }

    [Fact]
    public void GetDiatonicChord_CMajor_CorrectQualities()
    {
        var key = Key.Parse("C");

        Assert.Equal(ChordQuality.Major, key.GetDiatonicChord(1).Quality);      // C
        Assert.Equal(ChordQuality.Minor, key.GetDiatonicChord(2).Quality);      // Dm
        Assert.Equal(ChordQuality.Minor, key.GetDiatonicChord(3).Quality);      // Em
        Assert.Equal(ChordQuality.Major, key.GetDiatonicChord(4).Quality);      // F
        Assert.Equal(ChordQuality.Major, key.GetDiatonicChord(5).Quality);      // G
        Assert.Equal(ChordQuality.Minor, key.GetDiatonicChord(6).Quality);      // Am
        Assert.Equal(ChordQuality.Diminished, key.GetDiatonicChord(7).Quality); // Bdim
    }

    [Fact]
    public void GetDiatonicChord_CMajor_CorrectRoots()
    {
        var key = Key.Parse("C");

        Assert.Equal("C", key.GetDiatonicChord(1).Root.Name);
        Assert.Equal("D", key.GetDiatonicChord(2).Root.Name);
        Assert.Equal("E", key.GetDiatonicChord(3).Root.Name);
        Assert.Equal("F", key.GetDiatonicChord(4).Root.Name);
        Assert.Equal("G", key.GetDiatonicChord(5).Root.Name);
        Assert.Equal("A", key.GetDiatonicChord(6).Root.Name);
        Assert.Equal("B", key.GetDiatonicChord(7).Root.Name);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        Assert.Equal("C", Key.Parse("C").ToString());
        Assert.Equal("Am", Key.Parse("Am").ToString());
    }
}
