using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;
using NAudio.Midi;

namespace ChipCraft.Engine.Tests.Midi;

public class MidiExporterTests : IDisposable
{
    private readonly MidiExporter _exporter = new();
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "chipcraft_midi_tests", Guid.NewGuid().ToString());

    public MidiExporterTests() => Directory.CreateDirectory(_outputDir);

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    [Fact]
    public void Export_CreatesFile()
    {
        var song = CreateTestSong();
        string path = Path.Combine(_outputDir, "test.mid");

        _exporter.Export(song, path);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 44);
    }

    [Fact]
    public void Export_ValidMidiHeader()
    {
        var song = CreateTestSong();
        var bytes = _exporter.ExportToBytes(song);

        // MIDI file starts with "MThd"
        Assert.Equal((byte)'M', bytes[0]);
        Assert.Equal((byte)'T', bytes[1]);
        Assert.Equal((byte)'h', bytes[2]);
        Assert.Equal((byte)'d', bytes[3]);
    }

    [Fact]
    public void Export_EmptySongProducesValidFile()
    {
        var song = new Song { Tempo = 120 };
        song.InitializeChannels(1);
        var pattern = new Pattern(16, 1);
        song.Patterns.Add(pattern);
        song.AddToOrder(0);

        var bytes = _exporter.ExportToBytes(song);
        Assert.True(bytes.Length > 14); // At least header
    }

    [Fact]
    public void Export_WithProgramChanges()
    {
        var song = CreateTestSong();
        song.SetChannelProgram(0, GeneralMidi.GetProgram(73)); // Flute

        var bytes = _exporter.ExportToBytes(song);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void Export_WithDrumChannel()
    {
        var song = new Song { Tempo = 120 };
        song.InitializeChannels(2);
        song.SetChannelProgram(0, GeneralMidi.GetProgram(0));
        song.SetDrumChannel(1);

        var pattern = new Pattern(16, 2);
        pattern.SetCell(0, 0, new PatternCell(Note.FromMidi(60), null, 12));
        pattern.SetCell(0, 1, new PatternCell(Note.FromMidi(36), null, 12)); // Kick drum
        pattern.SetCell(4, 1, new PatternCell(Note.FromMidi(38), null, 12)); // Snare
        song.Patterns.Add(pattern);
        song.AddToOrder(0);

        string path = Path.Combine(_outputDir, "drums.mid");
        _exporter.Export(song, path);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 100);
    }

    [Fact]
    public void Export_CreatesDirectoryIfNeeded()
    {
        var song = CreateTestSong();
        string path = Path.Combine(_outputDir, "sub", "dir", "test.mid");

        _exporter.Export(song, path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Export_WithMetadata_WritesExpressionAndTempoMap()
    {
        var song = CreateTestSong();
        song.Patterns.Add(song.Patterns[0]);
        song.AddToOrder(1);
        song.OrderList.Entries[1] = song.OrderList.Entries[1] with { TempoOverride = 132 };

        var metadata = new SongProjectMetadata(
            new CompositionSpec("Test", "", Genre.Action, Mood.Heroic, 8, false, "Am", ChipCraft.Engine.Theory.ScaleType.NaturalMinor, 120, "broad-gm", 1, 0.7f, "linear-arc"),
            new ArrangementPlan(8, false, "Intro / Climax",
            [
                new ArrangementSection("Intro", 0, 4, "intro", 0.58f, ["Am", "F", "C", "G"], "A"),
                new ArrangementSection("Climax", 4, 4, "climax", 0.82f, ["F", "G", "Am", "Em"], "B")
            ]),
            [
                new ChannelRoleAssignment(0, ChannelRole.Lead, "Acoustic Grand Piano", 0.8f, 0f),
                new ChannelRoleAssignment(1, ChannelRole.Bass, "Acoustic Bass", 0.75f, 0f)
            ]);

        string path = Path.Combine(_outputDir, "expressive.mid");
        _exporter.Export(song, path, metadata);

        var midiFile = new MidiFile(path, false);
        var controlChanges = midiFile.Events
            .SelectMany(track => track)
            .OfType<ControlChangeEvent>()
            .ToList();
        var tempoEvents = midiFile.Events
            .SelectMany(track => track)
            .OfType<TempoEvent>()
            .ToList();

        Assert.Contains(controlChanges, evt => evt.Controller == (MidiController)11);
        Assert.Contains(tempoEvents, evt => evt.AbsoluteTime > 0);
    }

    private static Song CreateTestSong()
    {
        var song = new Song { Title = "Test", Tempo = 120 };
        song.InitializeChannels(2);
        song.SetChannelProgram(0, GeneralMidi.GetProgram(0)); // Piano
        song.SetChannelProgram(1, GeneralMidi.GetProgram(32)); // Acoustic Bass

        var pattern = new Pattern(16, 2);
        // Melody: C4, E4, G4, C5
        pattern.SetCell(0, 0, new PatternCell(Note.FromMidi(60), null, 12));
        pattern.SetCell(4, 0, new PatternCell(Note.FromMidi(64), null, 12));
        pattern.SetCell(8, 0, new PatternCell(Note.FromMidi(67), null, 12));
        pattern.SetCell(12, 0, new PatternCell(Note.FromMidi(72), null, 12));
        // Bass: C2, G2
        pattern.SetCell(0, 1, new PatternCell(Note.FromMidi(36), null, 10));
        pattern.SetCell(8, 1, new PatternCell(Note.FromMidi(43), null, 10));

        song.Patterns.Add(pattern);
        song.AddToOrder(0);
        return song;
    }
}
