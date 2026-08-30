using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;

namespace ChipCraft.Engine.Tests.Midi;

public class GmDrumMapTests
{
    [Theory]
    [InlineData(DrumVoice.Kick, 36)]
    [InlineData(DrumVoice.Snare, 38)]
    [InlineData(DrumVoice.HiHatClosed, 42)]
    [InlineData(DrumVoice.HiHatOpen, 46)]
    [InlineData(DrumVoice.Crash, 49)]
    [InlineData(DrumVoice.Tom, 45)]
    public void GetMidiNote_ReturnsCorrectGmMapping(DrumVoice voice, int expectedNote)
    {
        Assert.Equal(expectedNote, GmDrumMap.GetMidiNote(voice));
    }

    [Fact]
    public void AllDrumVoices_MapToValidMidiRange()
    {
        foreach (var voice in Enum.GetValues<DrumVoice>())
        {
            int note = GmDrumMap.GetMidiNote(voice);
            Assert.InRange(note, 35, 81); // GM percussion range
        }
    }
}
