using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;

namespace ChipCraft.Engine.Sequencer;

public enum AutomationLaneType
{
    Expression,
    Modulation,
    Sustain,
    ReverbSend,
    ChorusSend,
    PitchBend
}

public record AutomationPoint(float Beat, float Value);

public class AutomationLane
{
    public AutomationLaneType Type { get; set; }
    public List<AutomationPoint> Points { get; set; } = [];
}

public record PartNote(
    Note Note,
    float StartBeat,
    float DurationBeats,
    byte Velocity = 100)
{
    public float EndBeat => StartBeat + DurationBeats;
}

public class Part
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Part";
    public int Channel { get; set; }
    public bool IsDrumPart { get; set; }
    public MidiProgram? ProgramOverride { get; set; }
    public List<PartNote> Notes { get; set; } = [];
    public List<AutomationLane> AutomationLanes { get; set; } = [];

    public AutomationLane GetOrCreateLane(AutomationLaneType type)
    {
        var lane = AutomationLanes.FirstOrDefault(existing => existing.Type == type);
        if (lane != null)
            return lane;

        lane = new AutomationLane { Type = type };
        AutomationLanes.Add(lane);
        return lane;
    }
}
