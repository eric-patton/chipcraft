using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;
using NAudio.Midi;

namespace ChipCraft.Engine.Tests.Midi;

public class MidiExporterExpressiveTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "chipcraft_midi_expressive_tests", Guid.NewGuid().ToString("N"));

    public MidiExporterExpressiveTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    [Fact]
    public void Export_WritesTimeSignaturePatchBindingsAndAutomation()
    {
        var song = new Song
        {
            Title = "Expressive",
            Tempo = 108,
            KeyName = "Dm",
            BeatsPerBar = 3,
            BeatUnit = 8,
            RowsPerBeat = 4
        };
        song.InitializeChannels(1);
        song.SetChannelProgram(0, GeneralMidi.GetProgram(0) with { Name = "Concert Grand", BankMsb = 5, BankLsb = 9 });
        song.ChannelReverbSends[0] = 48;
        song.ChannelChorusSends[0] = 12;

        var pattern = new Pattern(12, 1) { Name = "Meter Test" };
        var part = pattern.CreatePart(0, "Lead");
        part.ProgramOverride = GeneralMidi.GetProgram(40) with { Name = "Solo Violin Custom", BankMsb = 1, BankLsb = 2 };
        part.Notes =
        [
            new PartNote(Note.Parse("D4"), 0f, 1f, 96),
            new PartNote(Note.Parse("F4"), 1f, 1f, 88),
            new PartNote(Note.Parse("A4"), 2f, 1f, 104)
        ];
        part.AutomationLanes =
        [
            new AutomationLane
            {
                Type = AutomationLaneType.Expression,
                Points = [new AutomationPoint(0f, 76f), new AutomationPoint(2f, 110f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.Modulation,
                Points = [new AutomationPoint(0f, 8f), new AutomationPoint(1f, 42f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.Sustain,
                Points = [new AutomationPoint(0f, 127f), new AutomationPoint(2.5f, 0f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.ReverbSend,
                Points = [new AutomationPoint(0f, 52f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.ChorusSend,
                Points = [new AutomationPoint(0f, 21f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.PitchBend,
                Points = [new AutomationPoint(0.5f, -1024f), new AutomationPoint(1.5f, 1536f)]
            }
        ];

        song.Patterns.Add(pattern);
        song.AddToOrder(0);

        string path = Path.Combine(_outputDir, "expressive.mid");
        new MidiExporter().Export(song, path);

        var midiFile = new MidiFile(path, false);
        var timeSignature = midiFile.Events.SelectMany(track => track).OfType<TimeSignatureEvent>().Single();
        var controlChanges = midiFile.Events.SelectMany(track => track).OfType<ControlChangeEvent>().ToList();
        var pitchBends = midiFile.Events.SelectMany(track => track).OfType<PitchWheelChangeEvent>().ToList();
        var patchChanges = midiFile.Events.SelectMany(track => track).OfType<PatchChangeEvent>().ToList();

        Assert.Equal(3, timeSignature.Numerator);
        Assert.Equal("3/8", timeSignature.TimeSignature);
        Assert.Contains(controlChanges, evt => (int)evt.Controller == 0 && evt.ControllerValue == 5);
        Assert.Contains(controlChanges, evt => (int)evt.Controller == 32 && evt.ControllerValue == 9);
        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)91);
        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)93);
        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)11);
        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)1);
        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)64);
        Assert.NotEmpty(pitchBends);
        Assert.True(patchChanges.Count >= 2);
    }
}
