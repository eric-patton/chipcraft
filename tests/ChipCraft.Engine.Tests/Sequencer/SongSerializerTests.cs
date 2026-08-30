using System.Text.Json;
using System.Text.Json.Nodes;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Persistence;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Engine.Tests.Sequencer;

public class SongSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTripsMetadataPartsAndEffects()
    {
        var song = new Song
        {
            Title = "Serialize Me",
            Author = "Test Author",
            KeyName = "Em",
            Tempo = 96,
            BeatsPerBar = 5,
            BeatUnit = 8,
            RowsPerBeat = 6,
            MasterVolume = 0.82f
        };
        song.InitializeChannels(2);
        song.SetChannelProgram(0, GeneralMidi.GetProgram(0) with { Name = "Custom Piano", BankMsb = 2, BankLsb = 3 });
        song.SetChannelProgram(1, GeneralMidi.GetProgram(40));
        song.ChannelReverbSends[0] = 64;
        song.ChannelChorusSends[0] = 17;
        song.ChannelMutes[1] = true;

        var pattern = new Pattern(30, 2) { Name = "Verse" };
        pattern.SetCell(0, 0, new PatternCell(Note.Parse("E4"), null, 12, new EffectCommand(EffectCommandType.VolumeSlide, 0x2, 0x1)));
        pattern.SetCell(6, 0, new PatternCell(Note.Cut));

        var part = pattern.CreatePart(1, "Counterline");
        part.ProgramOverride = GeneralMidi.GetProgram(48) with { Name = "String Layer", BankMsb = 1, BankLsb = 9 };
        part.Notes =
        [
            new PartNote(Note.Parse("B3"), 0f, 1.5f, 94),
            new PartNote(Note.Parse("G3"), 2f, 1f, 88)
        ];
        part.AutomationLanes =
        [
            new AutomationLane
            {
                Type = AutomationLaneType.Expression,
                Points = [new AutomationPoint(0f, 70f), new AutomationPoint(2f, 104f)]
            },
            new AutomationLane
            {
                Type = AutomationLaneType.PitchBend,
                Points = [new AutomationPoint(1f, 512f)]
            }
        ];

        song.Patterns.Add(pattern);
        song.OrderList.Entries.Add(new OrderEntry(0, 112));
        song.OrderList.LoopStartIndex = 0;

        string json = SongSerializer.Serialize(song);
        Assert.DoesNotContain("SpeedOverride", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Speed\"", json, StringComparison.Ordinal);

        var loaded = SongSerializer.Deserialize(json);
        Assert.Equal("Serialize Me", loaded.Title);
        Assert.Equal("Test Author", loaded.Author);
        Assert.Equal("Em", loaded.KeyName);
        Assert.Equal(5, loaded.BeatsPerBar);
        Assert.Equal(8, loaded.BeatUnit);
        Assert.Equal(6, loaded.RowsPerBeat);
        Assert.Equal(64, loaded.ChannelReverbSends[0]);
        Assert.Equal(17, loaded.ChannelChorusSends[0]);
        Assert.True(loaded.ChannelMutes[1]);
        Assert.Equal(112, loaded.OrderList.Entries[0].TempoOverride);
        Assert.Equal(EffectCommandType.VolumeSlide, loaded.Patterns[0].GetCell(0, 0).Effect?.Type);
        Assert.Single(loaded.Patterns[0].Parts);
        Assert.Equal(2, loaded.Patterns[0].Parts[0].Notes.Count);
        Assert.Equal(2, loaded.Patterns[0].Parts[0].AutomationLanes.Count);
        Assert.Equal((byte)1, loaded.Patterns[0].Parts[0].ProgramOverride!.BankMsb);
    }

    [Fact]
    public void Deserialize_LegacySpeedFieldsLoadButDoNotReappearOnSave()
    {
        var song = new Song
        {
            Title = "Legacy",
            Tempo = 90,
            RowsPerBeat = 4,
            BeatsPerBar = 4,
            BeatUnit = 4
        };
        song.InitializeChannels(1);
        song.Patterns.Add(new Pattern(16, 1) { Name = "A" });
        song.OrderList.Entries.Add(new OrderEntry(0, 132));

        var root = JsonNode.Parse(SongSerializer.Serialize(song))!.AsObject();
        root["Speed"] = 6;
        root["OrderList"] = new JsonArray
        {
            new JsonObject
            {
                ["PatternIndex"] = 0,
                ["TempoOverride"] = 132,
                ["SpeedOverride"] = 3
            }
        };

        var loaded = SongSerializer.Deserialize(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Assert.Single(loaded.OrderList.Entries);
        Assert.Equal(132, loaded.OrderList.Entries[0].TempoOverride);

        string resaved = SongSerializer.Serialize(loaded);
        Assert.DoesNotContain("SpeedOverride", resaved, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Speed\"", resaved, StringComparison.Ordinal);
    }
}
