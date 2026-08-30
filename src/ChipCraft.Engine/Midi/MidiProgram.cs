namespace ChipCraft.Engine.Midi;

/// <summary>
/// A MIDI instrument assignment using General MIDI program numbers.
/// Replaces synthesis-specific instrument definitions with universal GM programs.
/// </summary>
public record MidiProgram(
    byte ProgramNumber,
    string Name,
    string Category,
    byte DefaultVolume = 100,
    byte DefaultPan = 64,
    byte ReverbSend = 40,
    byte ChorusSend = 0,
    byte BankMsb = 0,
    byte BankLsb = 0
)
{
    /// <summary>Sentinel value representing the GM Drum Kit (MIDI channel 10).</summary>
    public static readonly MidiProgram Drums = new(0, "Standard Drum Kit", "Drums");
}
