namespace ChipCraft.Engine.Sequencer;

public enum EffectCommandType
{
    None,
    Arpeggio,
    PitchSlideUp,
    PitchSlideDown,
    PortamentoToNote,
    Vibrato,
    Tremolo,
    VolumeSlide,
    SetVolume,
    NoteDelay,
    NoteCut,
    SetSpeed,
    SetDutyCycle
}

public record EffectCommand(EffectCommandType Type = EffectCommandType.None, byte ParamX = 0, byte ParamY = 0)
{
    public static readonly EffectCommand Empty = new();
    public bool IsEmpty => Type == EffectCommandType.None;
}
