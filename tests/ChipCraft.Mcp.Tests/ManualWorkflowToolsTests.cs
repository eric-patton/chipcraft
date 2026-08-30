using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Theory;
using ChipCraft.Mcp.State;
using ChipCraft.Mcp.Tools;

namespace ChipCraft.Mcp.Tests;

public class ManualWorkflowToolsTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "chipcraft_mcp_tests", Guid.NewGuid().ToString("N"));
    private readonly string _soundFontPath = ResolveTestSoundFont();

    public ManualWorkflowToolsTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    [Fact]
    public void ManualWorkflow_ListsMetadataExportsSavesAndLoadsSong()
    {
        var session = new SessionState();
        string versePatternId = CreatePattern(session, "Verse", 15, 3);
        string outroPatternId = CreatePattern(session, "Outro", 15, 3);

        SetNotes(session, versePatternId,
        [
            new { row = 0, channel = 0, note = "D4", volume = 12 },
            new { row = 5, channel = 0, note = "F4", volume = 12 },
            new { row = 10, channel = 0, note = "A4", volume = 11 },
            new { row = 0, channel = 1, note = "D2", volume = 11 },
            new { row = 10, channel = 1, note = "A1", volume = 10 },
            new { row = 0, channel = 2, note = "C2", volume = 12 },
            new { row = 1, channel = 2, note = "===" },
            new { row = 8, channel = 2, note = "D2", volume = 11 },
            new { row = 9, channel = 2, note = "===" }
        ]);

        SetNotes(session, outroPatternId,
        [
            new { row = 0, channel = 0, note = "A4", volume = 12 },
            new { row = 5, channel = 0, note = "C5", volume = 12 },
            new { row = 10, channel = 0, note = "D5", volume = 13 },
            new { row = 0, channel = 1, note = "G2", volume = 10 },
            new { row = 10, channel = 1, note = "D2", volume = 10 },
            new { row = 0, channel = 2, note = "C2", volume = 12 },
            new { row = 1, channel = 2, note = "===" }
        ]);

        string songId = CreateSong(session, "Manual Cue", tempo: 84, key: "Dm", channels: 3, author: "Test Composer", beatsPerBar: 3, beatUnit: 4, rowsPerBeat: 5);
        ProgramTools.SetChannelPatch(session, songId, 0, "Acoustic Grand Piano", bankMsb: 2, bankLsb: 4, name: "Stage Piano");
        ProgramTools.SetChannelProgram(session, songId, 1, "Acoustic Bass");
        ProgramTools.SetDrumChannel(session, songId, 2);
        SongTools.SetChannelMix(session, songId, 0, volume: 0.88f, pan: -0.15f, reverbSend: 54, chorusSend: 11);
        SongTools.SetChannelMix(session, songId, 1, volume: 0.73f, pan: 0.05f);
        SongTools.SetSongMetadata(session, songId, title: "Manual Cue Alt", author: "E. Patton", key: "Gm", masterVolume: 0.91f);
        CompositionTools.AddPatternToSong(session, songId, versePatternId);
        CompositionTools.AddPatternToSong(session, songId, outroPatternId);
        SongTools.SetSongTiming(session, songId, tempo: 92, loopPoint: 1);

        using var patternListDocument = JsonDocument.Parse(SessionTools.ListPatterns(session));
        Assert.Equal(2, patternListDocument.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(patternListDocument.RootElement.GetProperty("patterns").EnumerateArray(),
            pattern => pattern.GetProperty("name").GetString() == "Verse" && pattern.GetProperty("cellCount").GetInt32() > 0);

        using var songListDocument = JsonDocument.Parse(SessionTools.ListSongs(session));
        Assert.Equal(1, songListDocument.RootElement.GetProperty("count").GetInt32());
        var songSummary = songListDocument.RootElement.GetProperty("songs")[0];
        Assert.Equal("Manual Cue Alt", songSummary.GetProperty("title").GetString());
        Assert.Equal("E. Patton", songSummary.GetProperty("author").GetString());
        Assert.Equal("Gm", songSummary.GetProperty("key").GetString());
        Assert.Equal(3, songSummary.GetProperty("beatsPerBar").GetInt32());
        Assert.Equal(4, songSummary.GetProperty("beatUnit").GetInt32());
        Assert.Equal(5, songSummary.GetProperty("rowsPerBeat").GetInt32());

        using var stateDocument = JsonDocument.Parse(SongTools.GetSongState(session, songId));
        var root = stateDocument.RootElement;
        Assert.Equal("Manual Cue Alt", root.GetProperty("title").GetString());
        Assert.Equal("E. Patton", root.GetProperty("author").GetString());
        Assert.Equal("Gm", root.GetProperty("key").GetString());
        Assert.Equal(92, root.GetProperty("tempo").GetInt32());
        Assert.Equal(3, root.GetProperty("beatsPerBar").GetInt32());
        Assert.Equal(4, root.GetProperty("beatUnit").GetInt32());
        Assert.Equal(5, root.GetProperty("rowsPerBeat").GetInt32());
        Assert.Equal(1, root.GetProperty("loopPoint").GetInt32());
        Assert.DoesNotContain(root.EnumerateObject(), property => property.NameEquals("speed"));
        var channelPatch = root.GetProperty("channelPatches").EnumerateArray().First(patch => patch.GetProperty("channel").GetInt32() == 0);
        Assert.Equal(2, channelPatch.GetProperty("bankMsb").GetInt32());
        Assert.Equal(4, channelPatch.GetProperty("bankLsb").GetInt32());
        Assert.Equal("Stage Piano", channelPatch.GetProperty("name").GetString());

        string midiOutputPath = Path.Combine(_outputDir, "manual", "manual-cue.mid");
        using var midiDocument = JsonDocument.Parse(ExportTools.ExportMidi(session, songId, midiOutputPath));
        string exportedMidiPath = midiDocument.RootElement.GetProperty("outputPath").GetString()!;
        Assert.True(File.Exists(exportedMidiPath));

        string saveOutputDir = Path.Combine(_outputDir, "manual-save");
        using var saveDocument = JsonDocument.Parse(PersistenceTools.SaveSong(session, songId, saveOutputDir));
        string manifestPath = saveDocument.RootElement.GetProperty("ManifestPath").GetString()!;
        string songJsonPath = saveDocument.RootElement.GetProperty("SongJsonPath").GetString()!;
        string savedMidiPath = saveDocument.RootElement.GetProperty("MidiPath").GetString()!;
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(songJsonPath));
        Assert.True(File.Exists(savedMidiPath));

        string savedSongJson = File.ReadAllText(songJsonPath);
        Assert.DoesNotContain("SpeedOverride", savedSongJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Speed\"", savedSongJson, StringComparison.Ordinal);

        var loadSession = new SessionState();
        using var loadDocument = JsonDocument.Parse(PersistenceTools.LoadSong(loadSession, manifestPath));
        string loadedSongId = loadDocument.RootElement.GetProperty("songId").GetString()!;
        using var loadedStateDocument = JsonDocument.Parse(SongTools.GetSongState(loadSession, loadedSongId));
        var loadedState = loadedStateDocument.RootElement;
        Assert.Equal("Manual Cue Alt", loadedState.GetProperty("title").GetString());
        Assert.Equal("E. Patton", loadedState.GetProperty("author").GetString());
        Assert.Equal("Gm", loadedState.GetProperty("key").GetString());
        Assert.Equal(3, loadedState.GetProperty("beatsPerBar").GetInt32());
        Assert.Equal(4, loadedState.GetProperty("beatUnit").GetInt32());
        Assert.Equal(5, loadedState.GetProperty("rowsPerBeat").GetInt32());
    }

    [Fact]
    public void OrderEditingTools_UpdateOrderAndLoopPointDeterministically()
    {
        var session = new SessionState();
        string aPatternId = CreateSingleChannelPattern(session, "A", "C4");
        string bPatternId = CreateSingleChannelPattern(session, "B", "D4");
        string cPatternId = CreateSingleChannelPattern(session, "C", "E4");
        string dPatternId = CreateSingleChannelPattern(session, "D", "F4");
        string songId = CreateSong(session, "Arrange Me", tempo: 120, key: "Am", channels: 1);

        CompositionTools.AddPatternToSong(session, songId, aPatternId);
        CompositionTools.AddPatternToSong(session, songId, bPatternId);
        CompositionTools.AddPatternToSong(session, songId, dPatternId);
        SongTools.SetSongTiming(session, songId, loopPoint: 1);

        using (var insertDocument = JsonDocument.Parse(SongTools.InsertPatternToSong(session, songId, cPatternId, 1, repeat: 2)))
            Assert.Equal(3, insertDocument.RootElement.GetProperty("loopPoint").GetInt32());

        using (var removeBeforeLoopDocument = JsonDocument.Parse(SongTools.RemoveOrderEntry(session, songId, 0)))
            Assert.Equal(2, removeBeforeLoopDocument.RootElement.GetProperty("loopPoint").GetInt32());

        using (var removeLoopPointDocument = JsonDocument.Parse(SongTools.RemoveOrderEntry(session, songId, 2)))
            Assert.Equal(2, removeLoopPointDocument.RootElement.GetProperty("loopPoint").GetInt32());

        using (var moveLoopEntryDocument = JsonDocument.Parse(SongTools.MoveOrderEntry(session, songId, 2, 0)))
            Assert.Equal(0, moveLoopEntryDocument.RootElement.GetProperty("loopPoint").GetInt32());

        using (var replaceDocument = JsonDocument.Parse(SongTools.ReplaceOrderEntryPattern(session, songId, 1, bPatternId)))
            Assert.Equal(0, replaceDocument.RootElement.GetProperty("loopPoint").GetInt32());

        SongTools.SetSongTiming(session, songId, loopPoint: 1);
        using (var moveAcrossLoopDocument = JsonDocument.Parse(SongTools.MoveOrderEntry(session, songId, 2, 0)))
            Assert.Equal(2, moveAcrossLoopDocument.RootElement.GetProperty("loopPoint").GetInt32());

        using var stateDocument = JsonDocument.Parse(SongTools.GetSongState(session, songId));
        var orderEntries = stateDocument.RootElement.GetProperty("orderEntries").EnumerateArray()
            .Select(entry => entry.GetProperty("patternName").GetString()!)
            .ToArray();
        Assert.Equal(["C", "D", "B"], orderEntries);
    }

    [Fact]
    public void PartTools_RoundTripGridOperationsAndPatternUtilitiesPreservePartData()
    {
        var session = new SessionState();
        string patternId = CreatePattern(session, "Part Source", 16, 2);
        SetNotes(session, patternId,
        [
            new { row = 0, channel = 0, note = "C4", volume = 12 },
            new { row = 4, channel = 0, note = "E4", volume = 12 },
            new { row = 8, channel = 0, note = "G4", volume = 12 },
            new { row = 12, channel = 0, note = "C5", volume = 13 }
        ]);

        string songId = CreateSong(session, "Parts", tempo: 100, key: "C", channels: 2);
        CompositionTools.AddPatternToSong(session, songId, patternId);

        using var createPartDocument = JsonDocument.Parse(PartTools.CreatePart(session, patternId, 1, "Legato Strings"));
        string partId = createPartDocument.RootElement.GetProperty("partId").GetString()!;

        using var setNotesDocument = JsonDocument.Parse(PartTools.SetPartNotes(session, partId, JsonSerializer.Serialize(new object[]
        {
            new { startBeat = 0f, durationBeats = 1.5f, note = "G3", velocity = 90 },
            new { startBeat = 2f, durationBeats = 1f, note = "A3", velocity = 84 }
        })));
        Assert.Equal(2, setNotesDocument.RootElement.GetProperty("noteCount").GetInt32());

        using var setAutomationDocument = JsonDocument.Parse(PartTools.SetPartAutomation(session, partId, "expression", JsonSerializer.Serialize(new object[]
        {
            new { beat = 0f, value = 80f },
            new { beat = 2f, value = 110f }
        })));
        Assert.Equal(2, setAutomationDocument.RootElement.GetProperty("pointCount").GetInt32());

        using var overrideDocument = JsonDocument.Parse(PartTools.SetPartProgramOverride(session, partId, "String Ensemble 1", bankMsb: 1, bankLsb: 7, name: "Warm Strings"));
        Assert.Equal("Warm Strings", overrideDocument.RootElement.GetProperty("programOverride").GetProperty("name").GetString());

        using var listPartsDocument = JsonDocument.Parse(PartTools.ListParts(session, patternId));
        var partSummary = listPartsDocument.RootElement.GetProperty("parts")[0];
        Assert.Equal(2, partSummary.GetProperty("noteCount").GetInt32());
        Assert.Equal(1, partSummary.GetProperty("automationLaneCount").GetInt32());

        CompositionTools.ClearChannel(session, patternId, 0);
        using var afterClearDocument = JsonDocument.Parse(PartTools.ListParts(session, patternId));
        Assert.Equal(2, afterClearDocument.RootElement.GetProperty("parts")[0].GetProperty("noteCount").GetInt32());

        using var copyDocument = JsonDocument.Parse(CompositionTools.CopyPattern(session, patternId, "Part Copy"));
        string copyPatternId = copyDocument.RootElement.GetProperty("patternId").GetString()!;
        using var copiedPartsDocument = JsonDocument.Parse(PartTools.ListParts(session, copyPatternId));
        Assert.Equal(1, copiedPartsDocument.RootElement.GetProperty("count").GetInt32());

        using var transposedDocument = JsonDocument.Parse(CompositionTools.TransposePattern(session, patternId, 2, "Part Transposed"));
        string transposedPatternId = transposedDocument.RootElement.GetProperty("patternId").GetString()!;
        using var transposedPartsDocument = JsonDocument.Parse(PartTools.ListParts(session, transposedPatternId));
        Assert.Equal(1, transposedPartsDocument.RootElement.GetProperty("count").GetInt32());

        string mergeLeftId = CreatePattern(session, "Merge Left", 16, 1);
        string mergeRightId = CreatePattern(session, "Merge Right", 16, 1);
        using var mergePartDocument = JsonDocument.Parse(PartTools.CreatePart(session, mergeLeftId, 0, "Merged Pad"));
        string mergePartId = mergePartDocument.RootElement.GetProperty("partId").GetString()!;
        PartTools.SetPartNotes(session, mergePartId, JsonSerializer.Serialize(new object[]
        {
            new { startBeat = 0f, durationBeats = 2f, note = "C3", velocity = 88 }
        }));
        using var mergeDocument = JsonDocument.Parse(CompositionTools.MergePatterns(session, $"{mergeLeftId},{mergeRightId}", "Merged Parts"));
        string mergedPatternId = mergeDocument.RootElement.GetProperty("patternId").GetString()!;
        using var mergedPartsDocument = JsonDocument.Parse(PartTools.ListParts(session, mergedPatternId));
        Assert.Equal(1, mergedPartsDocument.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(0, mergedPartsDocument.RootElement.GetProperty("parts")[0].GetProperty("channel").GetInt32());

        string saveOutputDir = Path.Combine(_outputDir, "parts-save");
        using var saveDocument = JsonDocument.Parse(PersistenceTools.SaveSong(session, songId, saveOutputDir));
        string manifestPath = saveDocument.RootElement.GetProperty("ManifestPath").GetString()!;

        var loadSession = new SessionState();
        using var loadDocument = JsonDocument.Parse(PersistenceTools.LoadSong(loadSession, manifestPath));
        string loadedSongId = loadDocument.RootElement.GetProperty("songId").GetString()!;
        using var loadedStateDocument = JsonDocument.Parse(SongTools.GetSongState(loadSession, loadedSongId));
        var loadedPattern = loadedStateDocument.RootElement.GetProperty("patterns").EnumerateArray()
            .First(pattern => pattern.GetProperty("name").GetString() == "Part Source");
        Assert.Equal(1, loadedPattern.GetProperty("partCount").GetInt32());
    }

    [Fact]
    public void DeliveryAndReviewTools_RenderBundleAndAnalyzeOutputs()
    {
        Assert.True(File.Exists(_soundFontPath), $"Expected test SoundFont at '{_soundFontPath}'.");

        var session = new SessionState();
        string patternId = CreatePattern(session, "Delivery", 16, 1);
        string songId = CreateSong(session, "Delivery Cue", tempo: 88, key: "Dm", channels: 1, author: "Bundle Test");
        ProgramTools.SetChannelPatch(session, songId, 0, "Acoustic Grand Piano", bankMsb: 0, bankLsb: 0, name: "Concert Grand");
        CompositionTools.AddPatternToSong(session, songId, patternId);
        SongTools.SetSongTiming(session, songId, loopPoint: 0);

        using var createPartDocument = JsonDocument.Parse(PartTools.CreatePart(session, patternId, 0, "Piano Lead"));
        string partId = createPartDocument.RootElement.GetProperty("partId").GetString()!;
        PartTools.SetPartNotes(session, partId, JsonSerializer.Serialize(new object[]
        {
            new { startBeat = 0f, durationBeats = 1f, note = "D4", velocity = 98 },
            new { startBeat = 1f, durationBeats = 1f, note = "F4", velocity = 94 },
            new { startBeat = 2f, durationBeats = 1f, note = "A4", velocity = 101 },
            new { startBeat = 3f, durationBeats = 1f, note = "D5", velocity = 105 }
        }));
        PartTools.SetPartAutomation(session, partId, "expression", JsonSerializer.Serialize(new object[]
        {
            new { beat = 0f, value = 72f },
            new { beat = 3f, value = 108f }
        }));

        string stemsDir = Path.Combine(_outputDir, "stems");
        using var stemsDocument = JsonDocument.Parse(ExportTools.RenderStems(session, songId, _soundFontPath, stemsDir));
        Assert.Equal(1, stemsDocument.RootElement.GetProperty("count").GetInt32());
        string stemWavPath = stemsDocument.RootElement.GetProperty("stems")[0].GetProperty("wavPath").GetString()!;
        Assert.True(File.Exists(stemWavPath));

        string loopPreviewPath = Path.Combine(_outputDir, "delivery-loop.wav");
        using var loopDocument = JsonDocument.Parse(ExportTools.RenderLoopPreview(session, songId, _soundFontPath, loopPreviewPath, seamBars: 1));
        Assert.True(File.Exists(loopDocument.RootElement.GetProperty("outputPath").GetString()!));

        string bundleDir = Path.Combine(_outputDir, "bundle");
        using var bundleDocument = JsonDocument.Parse(ExportTools.ExportDeliveryBundle(session, songId, _soundFontPath, bundleDir));
        string manifestPath = bundleDocument.RootElement.GetProperty("manifestPath").GetString()!;
        string fullMixPath = bundleDocument.RootElement.GetProperty("fullMixPath").GetString()!;
        string loopBundlePath = bundleDocument.RootElement.GetProperty("loopPreviewPath").GetString()!;
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(fullMixPath));
        Assert.True(File.Exists(loopBundlePath));
        Assert.Equal(1, bundleDocument.RootElement.GetProperty("stemCount").GetInt32());

        using var renderAnalysisDocument = JsonDocument.Parse(ReviewTools.AnalyzeRender(fullMixPath));
        Assert.True(renderAnalysisDocument.RootElement.GetProperty("OverallScore").GetDouble() > 0);

        using var bundleReviewDocument = JsonDocument.Parse(ReviewTools.ReviewDeliveryBundle(bundleDir));
        Assert.Empty(bundleReviewDocument.RootElement.GetProperty("missingFiles").EnumerateArray());
        Assert.True(bundleReviewDocument.RootElement.GetProperty("renderAnalysis").GetProperty("OverallScore").GetDouble() > 0);

        using var songReviewDocument = JsonDocument.Parse(ReviewTools.ReviewSong(session, songId, _soundFontPath));
        Assert.True(songReviewDocument.RootElement.GetProperty("songAnalysis").GetProperty("ExportReadiness").GetProperty("Score").GetDouble() >= 0.8);
        Assert.True(songReviewDocument.RootElement.GetProperty("renderAnalysis").GetProperty("OverallScore").GetDouble() > 0);
        Assert.True(songReviewDocument.RootElement.TryGetProperty("deliveryReady", out _));

        string partPreviewPath = Path.Combine(_outputDir, "part-preview.wav");
        using var partRenderDocument = JsonDocument.Parse(ExportTools.RenderPartAudio(session, partId, _soundFontPath, partPreviewPath, songId));
        Assert.True(File.Exists(partRenderDocument.RootElement.GetProperty("outputPath").GetString()!));
    }

    [Fact]
    public void ValidationAndLegacyLoadBehavior_AreCleanAndCompatible()
    {
        var session = new SessionState();
        string patternId = CreateSingleChannelPattern(session, "Legacy", "A4");
        string songId = CreateSong(session, "Legacy Cue", tempo: 90, key: "Am", channels: 1);
        CompositionTools.AddPatternToSong(session, songId, patternId);

        using var invalidChannelDocument = JsonDocument.Parse(ProgramTools.SetChannelProgram(session, songId, 9, "Acoustic Grand Piano"));
        Assert.Contains("out of range", invalidChannelDocument.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);

        using var invalidLoopDocument = JsonDocument.Parse(SongTools.SetSongTiming(session, songId, loopPoint: 5));
        Assert.Contains("out of range", invalidLoopDocument.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);

        var spec = new CompositionSpec("Legacy Cue", "", Genre.Fantasy, Mood.Calm, 8, true, "Am", ScaleType.NaturalMinor, 90, "ambient", 77, 0.42f, "loop-variation");
        var arrangement = new ArrangementPlan(8, true, "A / A'",
        [
            new ArrangementSection("A", 0, 4, "statement", 0.4f, ["Am", "F", "C", "G"], "A"),
            new ArrangementSection("A'", 4, 4, "variation", 0.52f, ["Am", "F", "C", "G"], "A", "A")
        ]);
        session.SetSongMetadata(songId, new SongProjectMetadata(
            spec,
            arrangement,
            [new ChannelRoleAssignment(0, ChannelRole.Lead, "Acoustic Grand Piano", 0.8f, 0f)],
            Warnings: ["legacy manifest"]));

        string saveOutputDir = Path.Combine(_outputDir, "legacy-save");
        using var saveDocument = JsonDocument.Parse(PersistenceTools.SaveSong(session, songId, saveOutputDir));
        string manifestPath = saveDocument.RootElement.GetProperty("ManifestPath").GetString()!;
        string songJsonPath = saveDocument.RootElement.GetProperty("SongJsonPath").GetString()!;

        var root = JsonNode.Parse(File.ReadAllText(songJsonPath))!.AsObject();
        root["Speed"] = 6;
        var orderList = root["OrderList"]!.AsArray();
        orderList[0] = new JsonObject
        {
            ["PatternIndex"] = 0,
            ["SpeedOverride"] = 3
        };
        File.WriteAllText(songJsonPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var loadSession = new SessionState();
        using var loadDocument = JsonDocument.Parse(PersistenceTools.LoadSong(loadSession, manifestPath));
        string loadedSongId = loadDocument.RootElement.GetProperty("songId").GetString()!;
        var loadedMetadata = loadSession.GetSongMetadata(loadedSongId);
        Assert.NotNull(loadedMetadata);
        Assert.Equal("ambient", loadedMetadata!.Spec!.Palette);
        Assert.Equal("A / A'", loadedMetadata.ArrangementPlan!.Form);
        Assert.Contains("legacy manifest", loadedMetadata.WarningList);

        using var stateDocument = JsonDocument.Parse(SongTools.GetSongState(loadSession, loadedSongId));
        Assert.DoesNotContain(stateDocument.RootElement.EnumerateObject(), property => property.NameEquals("speed"));
        Assert.DoesNotContain(stateDocument.RootElement.GetProperty("orderEntries")[0].EnumerateObject(), property => property.NameEquals("speedOverride"));
    }

    [Fact]
    public void ReviewAndAssemblySurface_StayManualFirst()
    {
        var session = new SessionState();
        string patternId = CreatePattern(session, "Review", 12, 2);
        SetNotes(session, patternId,
        [
            new { row = 0, channel = 0, note = "G4", volume = 12 },
            new { row = 4, channel = 0, note = "A4", volume = 12 },
            new { row = 8, channel = 0, note = "C5", volume = 13 },
            new { row = 0, channel = 1, note = "C3", volume = 11 },
            new { row = 8, channel = 1, note = "G2", volume = 10 }
        ]);

        string songId = CreateSong(session, "Review Me", tempo: 88, key: "C", channels: 2, beatsPerBar: 3, beatUnit: 4);
        ProgramTools.SetChannelProgram(session, songId, 0, "Acoustic Grand Piano");
        ProgramTools.SetChannelProgram(session, songId, 1, "String Ensemble 1");
        CompositionTools.AddPatternToSong(session, songId, patternId);

        using var analysisDocument = JsonDocument.Parse(ReviewTools.AnalyzeSong(session, songId));
        double roleCoverage = analysisDocument.RootElement.GetProperty("RoleCoverage").GetProperty("Score").GetDouble();
        double exportReadiness = analysisDocument.RootElement.GetProperty("ExportReadiness").GetProperty("Score").GetDouble();
        Assert.True(roleCoverage >= 0.8);
        Assert.True(exportReadiness >= 0.8);

        using var explanationDocument = JsonDocument.Parse(ReviewTools.ExplainSong(session, songId));
        string explanation = explanationDocument.RootElement.GetProperty("explanation").GetString()!;
        Assert.Contains("Review Me", explanation);
        Assert.Contains("3/4", explanation);

        var toolNames = typeof(SongTools).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .SelectMany(method => method.CustomAttributes
                .Where(attribute => attribute.AttributeType.Name == "McpServerToolAttribute")
                .Select(attribute => attribute.NamedArguments.FirstOrDefault(argument => argument.MemberName == "Name").TypedValue.Value as string))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.DoesNotContain("compose_song", toolNames);
        Assert.DoesNotContain("revise_song", toolNames);
        Assert.DoesNotContain("set_order_entry_speed", toolNames);
        Assert.Contains("create_part", toolNames);
        Assert.Contains("render_stems", toolNames);
        Assert.Contains("render_loop_preview", toolNames);
        Assert.Contains("export_delivery_bundle", toolNames);
        Assert.Contains("analyze_render", toolNames);
        Assert.Contains("review_song", toolNames);
    }

    private static string CreateSong(
        SessionState session,
        string title,
        int tempo,
        string key,
        int channels,
        string? author = null,
        int beatsPerBar = 4,
        int beatUnit = 4,
        int rowsPerBeat = 4)
    {
        using var document = JsonDocument.Parse(SongTools.CreateSong(session, title, tempo, key, channels, author, beatsPerBar, beatUnit, rowsPerBeat));
        return document.RootElement.GetProperty("songId").GetString()!;
    }

    private static string CreatePattern(SessionState session, string name, int rows, int channels)
    {
        using var document = JsonDocument.Parse(CompositionTools.CreatePattern(session, rows, channels, name));
        return document.RootElement.GetProperty("patternId").GetString()!;
    }

    private static string CreateSingleChannelPattern(SessionState session, string name, string note)
    {
        string patternId = CreatePattern(session, name, 16, 1);
        SetNotes(session, patternId,
        [
            new { row = 0, channel = 0, note, volume = 12 },
            new { row = 4, channel = 0, note = "===" }
        ]);
        return patternId;
    }

    private static void SetNotes(SessionState session, string patternId, object[] notes)
    {
        string json = CompositionTools.SetNotes(session, patternId, JsonSerializer.Serialize(notes));
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("error", out _), json);
    }

    private static string ResolveTestSoundFont()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "soundfonts", "FluidR3_GM2-2.SF2");
    }
}
