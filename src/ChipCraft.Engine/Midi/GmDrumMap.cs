using ChipCraft.Engine.Generation;

namespace ChipCraft.Engine.Midi;

/// <summary>
/// Maps DrumVoice enum values to correct General MIDI percussion note numbers.
/// GM drums are always on MIDI channel 10 (index 9).
/// </summary>
public static class GmDrumMap
{
    public static int GetMidiNote(DrumVoice voice) => voice switch
    {
        DrumVoice.Kick => 36,         // Bass Drum 1
        DrumVoice.Snare => 38,        // Acoustic Snare
        DrumVoice.HiHatClosed => 42,  // Closed Hi-Hat
        DrumVoice.HiHatOpen => 46,    // Open Hi-Hat
        DrumVoice.Crash => 49,        // Crash Cymbal 1
        DrumVoice.Tom => 45,          // Low Tom
        _ => 38
    };
}
