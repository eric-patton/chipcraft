using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class PartTools
{
    [McpServerTool(Name = "create_part"), Description("Create a new expressive part on a pattern channel for note-event and automation authoring.")]
    public static string CreatePart(
        SessionState session,
        [Description("Pattern ID.")] string patternId,
        [Description("Channel index for this part.")] int channel,
        [Description("Optional part name.")] string? name = null,
        [Description("Whether this part should be treated as a drum/percussion part.")] bool isDrumPart = false)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });
        if (channel < 0 || channel >= pattern.ChannelCount)
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{pattern.ChannelCount - 1})." });

        var part = pattern.CreatePart(channel, name, isDrumPart);
        return JsonSerializer.Serialize(new
        {
            patternId,
            partId = part.Id,
            name = part.Name,
            channel = part.Channel,
            isDrumPart = part.IsDrumPart
        });
    }

    [McpServerTool(Name = "list_parts"), Description("List expressive parts currently stored inside a pattern.")]
    public static string ListParts(
        SessionState session,
        [Description("Pattern ID.")] string patternId)
    {
        var pattern = session.GetPattern(patternId);
        if (pattern == null)
            return JsonSerializer.Serialize(new { error = $"Pattern '{patternId}' not found." });

        var parts = pattern.Parts.Select(part => new
        {
            partId = part.Id,
            name = part.Name,
            channel = part.Channel,
            isDrumPart = part.IsDrumPart,
            noteCount = part.Notes.Count,
            automationLaneCount = part.AutomationLanes.Count,
            programOverride = part.ProgramOverride == null
                ? null
                : new
                {
                    program = part.ProgramOverride.ProgramNumber,
                    name = part.ProgramOverride.Name,
                    bankMsb = part.ProgramOverride.BankMsb,
                    bankLsb = part.ProgramOverride.BankLsb
                }
        });

        return JsonSerializer.Serialize(new { patternId, count = pattern.Parts.Count, parts });
    }

    [McpServerTool(Name = "set_part_notes"), Description("Replace the note list for one expressive part. Provide a JSON array of note events.")]
    public static string SetPartNotes(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("""JSON array of notes: [{"startBeat":0,"durationBeats":1,"note":"C4","velocity":100}, ...].""")] string notes)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });

        var noteInputs = JsonSerializer.Deserialize<List<PartNoteInput>>(notes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (noteInputs == null)
            return JsonSerializer.Serialize(new { error = "Invalid part notes JSON." });

        float patternBeats = ResolvePatternBeats(session, pattern);
        var noteList = new List<PartNote>();
        foreach (var input in noteInputs)
        {
            if (input.DurationBeats <= 0)
                return JsonSerializer.Serialize(new { error = "Part note durations must be greater than 0." });
            if (input.StartBeat < 0)
                return JsonSerializer.Serialize(new { error = "Part note start beats must be non-negative." });
            if (input.StartBeat + input.DurationBeats > patternBeats + 0.0001f)
                return JsonSerializer.Serialize(new { error = $"Part note at beat {input.StartBeat} exceeds the pattern length of {patternBeats:0.###} beats." });

            Note note;
            try
            {
                note = Note.Parse(input.Note);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid note '{input.Note}': {ex.Message}" });
            }

            noteList.Add(new PartNote(
                note,
                input.StartBeat,
                input.DurationBeats,
                (byte)Math.Clamp(input.Velocity ?? 100, 1, 127)));
        }

        part.Notes = noteList
            .OrderBy(note => note.StartBeat)
            .ThenBy(note => note.Note.MidiNumber)
            .ToList();

        return JsonSerializer.Serialize(new { partId, noteCount = part.Notes.Count });
    }

    [McpServerTool(Name = "set_part_automation"), Description("Replace one automation lane on an expressive part. Provide a JSON array of automation points.")]
    public static string SetPartAutomation(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Automation lane: expression, modulation, sustain, reverbSend, chorusSend, or pitchBend.")] string lane,
        [Description("""JSON array of points: [{"beat":0,"value":96}, {"beat":1.5,"value":110}].""")] string points)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (!TryParseLane(lane, out var laneType))
            return JsonSerializer.Serialize(new { error = $"Automation lane '{lane}' is not supported." });

        var pointInputs = JsonSerializer.Deserialize<List<AutomationPointInput>>(points, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (pointInputs == null)
            return JsonSerializer.Serialize(new { error = "Invalid automation JSON." });

        float patternBeats = ResolvePatternBeats(session, pattern);
        var automationPoints = new List<AutomationPoint>();
        foreach (var input in pointInputs)
        {
            if (input.Beat < 0 || input.Beat > patternBeats + 0.0001f)
                return JsonSerializer.Serialize(new { error = $"Automation point at beat {input.Beat} exceeds the pattern length of {patternBeats:0.###} beats." });

            float normalizedValue = NormalizeAutomationValue(laneType, input.Value);
            automationPoints.Add(new AutomationPoint(input.Beat, normalizedValue));
        }

        var laneState = part.GetOrCreateLane(laneType);
        laneState.Points = automationPoints.OrderBy(point => point.Beat).ToList();

        return JsonSerializer.Serialize(new { partId, lane = laneState.Type.ToString(), pointCount = laneState.Points.Count });
    }

    [McpServerTool(Name = "set_part_program_override"), Description("Assign or clear a patch override for a specific expressive part.")]
    public static string SetPartProgramOverride(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("GM program number (0-127) or program name used as the override base. Omit to clear the override.")] string? program = null,
        [Description("Optional bank MSB value (0-127).")] int? bankMsb = null,
        [Description("Optional bank LSB value (0-127).")] int? bankLsb = null,
        [Description("Optional patch display name override.")] string? name = null)
    {
        if (!session.TryGetPart(partId, out _, out var part) || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });

        if (string.IsNullOrWhiteSpace(program))
        {
            part.ProgramOverride = null;
            return JsonSerializer.Serialize(new { partId, programOverride = (object?)null });
        }

        MidiProgram? baseProgram = ResolveProgram(program);
        if (baseProgram == null)
            return JsonSerializer.Serialize(new { error = $"Program '{program}' not found. Use list_gm_programs to see available programs." });

        part.ProgramOverride = baseProgram with
        {
            Name = string.IsNullOrWhiteSpace(name) ? baseProgram.Name : name,
            BankMsb = (byte)Math.Clamp(bankMsb ?? baseProgram.BankMsb, 0, 127),
            BankLsb = (byte)Math.Clamp(bankLsb ?? baseProgram.BankLsb, 0, 127)
        };

        return JsonSerializer.Serialize(new
        {
            partId,
            programOverride = new
            {
                program = part.ProgramOverride.ProgramNumber,
                name = part.ProgramOverride.Name,
                bankMsb = part.ProgramOverride.BankMsb,
                bankLsb = part.ProgramOverride.BankLsb
            }
        });
    }

    [McpServerTool(Name = "delete_part_range"), Description("Delete notes and/or automation points that intersect a beat range on an expressive part.")]
    public static string DeletePartRange(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Range start in beats.")] float startBeat,
        [Description("Range end in beats.")] float endBeat,
        [Description("Whether to delete intersecting notes.")] bool deleteNotes = true,
        [Description("Whether to delete automation points inside the range.")] bool deleteAutomation = true)
    {
        if (!session.TryGetPart(partId, out _, out var part) || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (!IsValidRange(startBeat, endBeat))
            return JsonSerializer.Serialize(new { error = "Beat range must satisfy 0 <= startBeat < endBeat." });

        int notesRemoved = 0;
        int pointsRemoved = 0;

        if (deleteNotes)
            notesRemoved = part.Notes.RemoveAll(note => note.StartBeat < endBeat && note.EndBeat > startBeat);

        if (deleteAutomation)
        {
            foreach (var lane in part.AutomationLanes)
                pointsRemoved += lane.Points.RemoveAll(point => point.Beat >= startBeat && point.Beat < endBeat);
        }

        return JsonSerializer.Serialize(new { partId, notesRemoved, automationPointsRemoved = pointsRemoved });
    }

    [McpServerTool(Name = "transpose_part_range"), Description("Transpose notes in a beat range on an expressive part by a number of semitones.")]
    public static string TransposePartRange(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Semitones to transpose.")] int semitones,
        [Description("Optional range start in beats. Omit to affect the whole part.")] float? startBeat = null,
        [Description("Optional range end in beats. Omit to affect the whole part.")] float? endBeat = null)
    {
        if (!session.TryGetPart(partId, out _, out var part) || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });

        var notes = TryFilterNotes(part, startBeat, endBeat, out var error);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        var selected = notes.ToList();
        foreach (var note in selected)
        {
            int index = part.Notes.IndexOf(note);
            int midi = Math.Clamp(note.Note.MidiNumber + semitones, 0, 127);
            part.Notes[index] = note with { Note = Note.FromMidi(midi) };
        }

        return JsonSerializer.Serialize(new { partId, notesAffected = selected.Count, semitones });
    }

    [McpServerTool(Name = "scale_part_velocities"), Description("Scale note velocities on an expressive part by a factor.")]
    public static string ScalePartVelocities(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Velocity scale factor, e.g. 0.8 or 1.15.")] float factor,
        [Description("Optional range start in beats. Omit to affect the whole part.")] float? startBeat = null,
        [Description("Optional range end in beats. Omit to affect the whole part.")] float? endBeat = null)
    {
        if (!session.TryGetPart(partId, out _, out var part) || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (factor <= 0)
            return JsonSerializer.Serialize(new { error = "Velocity scale factor must be greater than 0." });

        var notes = TryFilterNotes(part, startBeat, endBeat, out var error);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        var selected = notes.ToList();
        foreach (var note in selected)
        {
            int index = part.Notes.IndexOf(note);
            part.Notes[index] = note with { Velocity = (byte)Math.Clamp((int)Math.Round(note.Velocity * factor), 1, 127) };
        }

        return JsonSerializer.Serialize(new { partId, notesAffected = selected.Count, factor });
    }

    [McpServerTool(Name = "quantize_part"), Description("Quantize note start times on an expressive part to the nearest beat grid while preserving durations.")]
    public static string QuantizePart(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Quantization grid in beats, e.g. 0.25 for sixteenths or 0.5 for eighths.")] float gridBeats,
        [Description("Optional range start in beats. Omit to affect the whole part.")] float? startBeat = null,
        [Description("Optional range end in beats. Omit to affect the whole part.")] float? endBeat = null)
    {
        if (!session.TryGetPart(partId, out _, out var part) || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (gridBeats <= 0)
            return JsonSerializer.Serialize(new { error = "Quantization grid must be greater than 0 beats." });

        var notes = TryFilterNotes(part, startBeat, endBeat, out var error);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        var selected = notes.ToList();
        foreach (var note in selected)
        {
            int index = part.Notes.IndexOf(note);
            float snappedStart = MathF.Round(note.StartBeat / gridBeats) * gridBeats;
            part.Notes[index] = note with { StartBeat = Math.Max(0, snappedStart) };
        }

        part.Notes = part.Notes.OrderBy(note => note.StartBeat).ThenBy(note => note.Note.MidiNumber).ToList();
        return JsonSerializer.Serialize(new { partId, notesAffected = selected.Count, gridBeats });
    }

    [McpServerTool(Name = "humanize_part"), Description("Apply small timing and velocity variation to an expressive part.")]
    public static string HumanizePart(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Maximum timing offset in beats.")] float maxTimingOffsetBeats = 0.03f,
        [Description("Maximum velocity delta in MIDI units.")] int maxVelocityDelta = 6,
        [Description("Optional deterministic seed.")] int? seed = null,
        [Description("Optional range start in beats. Omit to affect the whole part.")] float? startBeat = null,
        [Description("Optional range end in beats. Omit to affect the whole part.")] float? endBeat = null)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (maxTimingOffsetBeats < 0 || maxVelocityDelta < 0)
            return JsonSerializer.Serialize(new { error = "Humanize offsets must be non-negative." });

        var notes = TryFilterNotes(part, startBeat, endBeat, out var error);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        float patternBeats = ResolvePatternBeats(session, pattern);
        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        var selected = notes.ToList();
        foreach (var note in selected)
        {
            int index = part.Notes.IndexOf(note);
            float offset = (float)((random.NextDouble() * 2.0 - 1.0) * maxTimingOffsetBeats);
            int velocityDelta = random.Next(-maxVelocityDelta, maxVelocityDelta + 1);
            float start = Math.Clamp(note.StartBeat + offset, 0f, Math.Max(0f, patternBeats - note.DurationBeats));
            byte velocity = (byte)Math.Clamp(note.Velocity + velocityDelta, 1, 127);
            part.Notes[index] = note with { StartBeat = start, Velocity = velocity };
        }

        part.Notes = part.Notes.OrderBy(note => note.StartBeat).ThenBy(note => note.Note.MidiNumber).ToList();
        return JsonSerializer.Serialize(new { partId, notesAffected = selected.Count, seed });
    }

    [McpServerTool(Name = "duplicate_part_range"), Description("Duplicate a beat range from an expressive part to a new destination beat.")]
    public static string DuplicatePartRange(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Range start in beats.")] float startBeat,
        [Description("Range end in beats.")] float endBeat,
        [Description("Destination start beat for the duplicated material.")] float destinationBeat)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (!IsValidRange(startBeat, endBeat) || destinationBeat < 0)
            return JsonSerializer.Serialize(new { error = "Range must satisfy 0 <= startBeat < endBeat and destinationBeat must be non-negative." });

        float patternBeats = ResolvePatternBeats(session, pattern);
        float delta = destinationBeat - startBeat;
        var notesToDuplicate = part.Notes.Where(note => note.StartBeat >= startBeat && note.EndBeat <= endBeat).ToList();
        if (notesToDuplicate.Any(note => note.EndBeat + delta > patternBeats + 0.0001f))
            return JsonSerializer.Serialize(new { error = $"Duplicated notes would exceed the pattern length of {patternBeats:0.###} beats." });

        part.Notes.AddRange(notesToDuplicate.Select(note => note with { StartBeat = note.StartBeat + delta }));
        foreach (var lane in part.AutomationLanes)
        {
            var copiedPoints = lane.Points
                .Where(point => point.Beat >= startBeat && point.Beat <= endBeat)
                .Select(point => new AutomationPoint(point.Beat + delta, point.Value));
            lane.Points.AddRange(copiedPoints);
            lane.Points = lane.Points.OrderBy(point => point.Beat).ToList();
        }

        part.Notes = part.Notes.OrderBy(note => note.StartBeat).ThenBy(note => note.Note.MidiNumber).ToList();
        return JsonSerializer.Serialize(new { partId, notesDuplicated = notesToDuplicate.Count, destinationBeat });
    }

    [McpServerTool(Name = "move_part_range"), Description("Move a fully-contained beat range on an expressive part to a new destination beat.")]
    public static string MovePartRange(
        SessionState session,
        [Description("Part ID.")] string partId,
        [Description("Range start in beats.")] float startBeat,
        [Description("Range end in beats.")] float endBeat,
        [Description("Destination start beat for the moved material.")] float destinationBeat)
    {
        if (!session.TryGetPart(partId, out var pattern, out var part) || pattern == null || part == null)
            return JsonSerializer.Serialize(new { error = $"Part '{partId}' not found." });
        if (!IsValidRange(startBeat, endBeat) || destinationBeat < 0)
            return JsonSerializer.Serialize(new { error = "Range must satisfy 0 <= startBeat < endBeat and destinationBeat must be non-negative." });

        float patternBeats = ResolvePatternBeats(session, pattern);
        float delta = destinationBeat - startBeat;
        var notesToMove = part.Notes.Where(note => note.StartBeat >= startBeat && note.EndBeat <= endBeat).ToList();
        if (notesToMove.Any(note => note.EndBeat + delta > patternBeats + 0.0001f))
            return JsonSerializer.Serialize(new { error = $"Moved notes would exceed the pattern length of {patternBeats:0.###} beats." });

        foreach (var note in notesToMove)
        {
            int index = part.Notes.IndexOf(note);
            part.Notes[index] = note with { StartBeat = note.StartBeat + delta };
        }

        foreach (var lane in part.AutomationLanes)
        {
            var pointsToMove = lane.Points.Where(point => point.Beat >= startBeat && point.Beat <= endBeat).ToList();
            foreach (var point in pointsToMove)
            {
                int index = lane.Points.IndexOf(point);
                lane.Points[index] = new AutomationPoint(point.Beat + delta, point.Value);
            }

            lane.Points = lane.Points.OrderBy(point => point.Beat).ToList();
        }

        part.Notes = part.Notes.OrderBy(note => note.StartBeat).ThenBy(note => note.Note.MidiNumber).ToList();
        return JsonSerializer.Serialize(new { partId, notesMoved = notesToMove.Count, destinationBeat });
    }

    private static IEnumerable<PartNote> TryFilterNotes(Part part, float? startBeat, float? endBeat, out string? error)
    {
        error = null;
        if (startBeat.HasValue != endBeat.HasValue)
        {
            error = "Start and end beat must both be supplied when filtering by range.";
            return [];
        }

        if (!startBeat.HasValue || !endBeat.HasValue)
            return part.Notes;

        if (!IsValidRange(startBeat.Value, endBeat.Value))
        {
            error = "Beat range must satisfy 0 <= startBeat < endBeat.";
            return [];
        }

        return part.Notes.Where(note => note.StartBeat < endBeat.Value && note.EndBeat > startBeat.Value);
    }

    private static float ResolvePatternBeats(SessionState session, Pattern pattern)
    {
        foreach (var song in session.ListSongs())
        {
            if (song.Patterns.Contains(pattern))
                return pattern.RowCount / (float)Math.Max(1, song.RowsPerBeat);
        }

        return pattern.RowCount / 4f;
    }

    private static float NormalizeAutomationValue(AutomationLaneType laneType, float value) =>
        laneType switch
        {
            AutomationLaneType.PitchBend => Math.Clamp(value, -8192f, 8191f),
            AutomationLaneType.Sustain => value >= 64f ? 127f : 0f,
            _ => Math.Clamp(value, 0f, 127f)
        };

    private static bool TryParseLane(string lane, out AutomationLaneType laneType)
    {
        laneType = lane.Trim().ToLowerInvariant() switch
        {
            "expression" => AutomationLaneType.Expression,
            "modulation" => AutomationLaneType.Modulation,
            "sustain" => AutomationLaneType.Sustain,
            "reverbsend" => AutomationLaneType.ReverbSend,
            "chorussend" => AutomationLaneType.ChorusSend,
            "pitchbend" => AutomationLaneType.PitchBend,
            _ => AutomationLaneType.Expression
        };

        return lane.Trim().ToLowerInvariant() is "expression" or "modulation" or "sustain" or "reverbsend" or "chorussend" or "pitchbend";
    }

    private static MidiProgram? ResolveProgram(string program)
    {
        if (byte.TryParse(program, out byte programNumber) && programNumber <= 127)
            return GeneralMidi.GetProgram(programNumber);

        return GeneralMidi.FindByName(program);
    }

    private static bool IsValidRange(float startBeat, float endBeat) =>
        startBeat >= 0 && endBeat > startBeat;

    private sealed record PartNoteInput(float StartBeat, float DurationBeats, string Note, int? Velocity = null);
    private sealed record AutomationPointInput(float Beat, float Value);
}
