using ChipCraft.Engine.Midi;

namespace ChipCraft.Engine.Tests.Midi;

public class GeneralMidiTests
{
    [Fact]
    public void All_Has128Programs()
    {
        Assert.Equal(128, GeneralMidi.All.Count);
    }

    [Theory]
    [InlineData(0, "Acoustic Grand Piano")]
    [InlineData(33, "Electric Bass Finger")]
    [InlineData(73, "Flute")]
    [InlineData(80, "Lead 1 Square")]
    public void GetProgram_ReturnsCorrectName(byte number, string expectedName)
    {
        var program = GeneralMidi.GetProgram(number);
        Assert.Equal(expectedName, program.Name);
        Assert.Equal(number, program.ProgramNumber);
    }

    [Fact]
    public void GetByCategory_ReturnsCorrectPrograms()
    {
        var pianos = GeneralMidi.GetByCategory("Piano");
        Assert.Equal(8, pianos.Count);
        Assert.All(pianos, p => Assert.Equal("Piano", p.Category));
    }

    [Fact]
    public void FindByName_FindsPrograms()
    {
        var flute = GeneralMidi.FindByName("Flute");
        Assert.NotNull(flute);
        Assert.Equal(73, flute.ProgramNumber);
    }

    [Fact]
    public void FindByName_PartialMatch()
    {
        var bass = GeneralMidi.FindByName("bass");
        Assert.NotNull(bass);
        Assert.Contains("Bass", bass.Name);
    }

    [Fact]
    public void Categories_NotEmpty()
    {
        Assert.True(GeneralMidi.Categories.Count >= 10);
    }
}
