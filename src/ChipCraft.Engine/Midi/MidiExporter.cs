using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Sequencer;
using NAudio.Midi;

namespace ChipCraft.Engine.Midi;

/// <summary>
/// Exports a Song to a Standard MIDI File (SMF Type 1, multi-track).
/// Each song channel becomes a MIDI track with program changes, mix setup,
/// automation, and note events derived from both tracker cells and expressive parts.
/// </summary>
public class MidiExporter
{
    private const int DeltaTicksPerQuarterNote = 480;
    private const MidiController ExpressionController = (MidiController)11;
    private const MidiController ReverbSendController = (MidiController)91;
    private const MidiController ChorusSendController = (MidiController)93;

    public void Export(Song song, string filePath, SongProjectMetadata? metadata = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var collection = BuildMidiEvents(song, metadata);
        MidiFile.Export(filePath, collection);
    }

    public byte[] ExportToBytes(Song song, SongProjectMetadata? metadata = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"chipcraft_{Guid.NewGuid():N}.mid");
        try
        {
            Export(song, tempPath, metadata);
            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static MidiEventCollection BuildMidiEvents(Song song, SongProjectMetadata? metadata = null)
    {
        int ticksPerRow = DeltaTicksPerQuarterNote / Math.Max(1, song.RowsPerBeat);
        var assignmentMap = (metadata?.ChannelAssignments ?? []).ToDictionary(assignment => assignment.Channel);
        var collection = new MidiEventCollection(1, DeltaTicksPerQuarterNote);

        collection.AddTrack();
        AddConductorTrack(collection, song);

        var drumChannelFlags = Enumerable.Range(0, song.ChannelCount)
            .Select(channel => IsDrumChannel(song, channel))
            .ToArray();
        var channelMap = BuildMidiChannelMap(song.ChannelCount, drumChannelFlags);

        for (int channel = 0; channel < song.ChannelCount; channel++)
        {
            collection.AddTrack();
            int trackIndex = channel + 1;
            int midiChannel = channelMap[channel];
            bool isDrumChannel = drumChannelFlags[channel];
            var basePatch = ResolveBasePatch(song, channel, isDrumChannel);
            var trackEvents = new List<MidiEvent>();

            if (!isDrumChannel)
                AddPatchEvents(trackEvents, 0, midiChannel, basePatch);

            AddChannelMixEvents(trackEvents, song, channel, midiChannel, basePatch);
            if (assignmentMap.TryGetValue(channel, out var assignment))
                AddExpressionEnvelope(trackEvents, midiChannel, song, metadata, assignment, ticksPerRow);

            int rowOffset = 0;
            for (int orderIndex = 0; orderIndex < song.OrderList.Entries.Count; orderIndex++)
            {
                var entry = song.OrderList.Entries[orderIndex];
                if (entry.PatternIndex < 0 || entry.PatternIndex >= song.Patterns.Count)
                    continue;

                var pattern = song.Patterns[entry.PatternIndex];
                long patternStartTick = (long)rowOffset * ticksPerRow;
                var section = metadata?.ArrangementPlan?.Sections.ElementAtOrDefault(orderIndex);

                AddGridEvents(trackEvents, pattern, channel, patternStartTick, song, midiChannel, assignmentMap.GetValueOrDefault(channel), section, isDrumChannel);
                AddPartEvents(trackEvents, pattern, channel, patternStartTick, song, midiChannel, basePatch, assignmentMap.GetValueOrDefault(channel), section, isDrumChannel);

                rowOffset += pattern.RowCount;
            }

            foreach (var midiEvent in trackEvents.OrderBy(evt => evt.AbsoluteTime).ThenBy(GetEventPriority))
                collection.AddEvent(midiEvent, trackIndex);

            long songEndTick = (long)rowOffset * ticksPerRow;
            collection.AddEvent(new MetaEvent(MetaEventType.EndTrack, 0, songEndTick), trackIndex);
        }

        return collection;
    }

    private static void AddConductorTrack(MidiEventCollection collection, Song song)
    {
        int microsecondsPerBeat = 60_000_000 / Math.Max(1, song.Tempo);
        collection.AddEvent(new TempoEvent(microsecondsPerBeat, 0), 0);

        int denominatorPower = (int)Math.Log2(Math.Max(1, song.BeatUnit));
        collection.AddEvent(new TimeSignatureEvent(0, song.BeatsPerBar, denominatorPower, 24, 8), 0);
        collection.AddEvent(new TextEvent("ChipCraft", MetaEventType.SequenceTrackName, 0), 0);

        int ticksPerRow = DeltaTicksPerQuarterNote / Math.Max(1, song.RowsPerBeat);
        int currentTempo = song.Tempo;
        int rowOffset = 0;
        foreach (var entry in song.OrderList.Entries)
        {
            if (entry.TempoOverride.HasValue && entry.TempoOverride.Value != currentTempo)
            {
                int overrideMicros = 60_000_000 / Math.Max(1, entry.TempoOverride.Value);
                collection.AddEvent(new TempoEvent(overrideMicros, (long)rowOffset * ticksPerRow), 0);
                currentTempo = entry.TempoOverride.Value;
            }

            if (entry.PatternIndex >= 0 && entry.PatternIndex < song.Patterns.Count)
                rowOffset += song.Patterns[entry.PatternIndex].RowCount;
        }

        int totalRows = song.OrderList.Entries
            .Where(entry => entry.PatternIndex >= 0 && entry.PatternIndex < song.Patterns.Count)
            .Sum(entry => song.Patterns[entry.PatternIndex].RowCount);
        long endTick = (long)totalRows * ticksPerRow;
        collection.AddEvent(new MetaEvent(MetaEventType.EndTrack, 0, endTick), 0);
    }

    private static int[] BuildMidiChannelMap(int channelCount, IReadOnlyList<bool> drumChannelFlags)
    {
        int nextMelodicChannel = 0;
        var channelMap = new int[channelCount];
        for (int channel = 0; channel < channelCount; channel++)
        {
            if (drumChannelFlags[channel])
            {
                channelMap[channel] = 9;
                continue;
            }

            if (nextMelodicChannel == 9)
                nextMelodicChannel++;

            channelMap[channel] = Math.Min(nextMelodicChannel, 15);
            nextMelodicChannel++;
        }

        return channelMap;
    }

    private static MidiProgram ResolveBasePatch(Song song, int channel, bool isDrumChannel)
    {
        if (song.ChannelPrograms.TryGetValue(channel, out var program))
            return program;

        return isDrumChannel ? MidiProgram.Drums : GeneralMidi.GetProgram(0);
    }

    private static void AddChannelMixEvents(List<MidiEvent> trackEvents, Song song, int channel, int midiChannel, MidiProgram patch)
    {
        byte volume = channel < song.ChannelVolumes.Length
            ? (byte)Math.Clamp((int)Math.Round(song.ChannelVolumes[channel] * 127), 0, 127)
            : patch.DefaultVolume;
        byte pan = channel < song.ChannelPans.Length
            ? (byte)Math.Clamp((int)Math.Round((song.ChannelPans[channel] + 1f) * 63.5f), 0, 127)
            : patch.DefaultPan;
        byte reverbSend = channel < song.ChannelReverbSends.Length ? song.ChannelReverbSends[channel] : patch.ReverbSend;
        byte chorusSend = channel < song.ChannelChorusSends.Length ? song.ChannelChorusSends[channel] : patch.ChorusSend;

        trackEvents.Add(new ControlChangeEvent(0, midiChannel + 1, MidiController.MainVolume, volume));
        trackEvents.Add(new ControlChangeEvent(0, midiChannel + 1, MidiController.Pan, pan));
        trackEvents.Add(new ControlChangeEvent(0, midiChannel + 1, ReverbSendController, reverbSend));
        trackEvents.Add(new ControlChangeEvent(0, midiChannel + 1, ChorusSendController, chorusSend));
    }

    private static void AddGridEvents(
        List<MidiEvent> trackEvents,
        Pattern pattern,
        int channel,
        long patternStartTick,
        Song song,
        int midiChannel,
        ChannelRoleAssignment? assignment,
        ArrangementSection? section,
        bool isDrumChannel)
    {
        if (channel >= pattern.ChannelCount || !song.IsChannelAudible(channel))
            return;

        var noteSequence = pattern.ToNoteSequence(channel, song.RowsPerBeat);
        foreach (var noteEvent in noteSequence.Events.Where(evt => !evt.IsRest))
        {
            long startTick = patternStartTick + BeatsToTicks(noteEvent.StartBeat);
            long endTick = patternStartTick + BeatsToTicks(noteEvent.EndBeat);
            int row = (int)Math.Round(noteEvent.StartBeat * song.RowsPerBeat);
            int baseVelocity = Math.Clamp((int)Math.Round(noteEvent.Velocity * 127), 1, 127);
            int velocity = ResolveVelocity(baseVelocity, noteEvent.Note.MidiNumber, row, song.RowsPerBeat, song.BeatsPerBar, isDrumChannel, assignment, section);

            trackEvents.Add(new NAudio.Midi.NoteEvent(startTick, midiChannel + 1, MidiCommandCode.NoteOn, noteEvent.Note.MidiNumber, velocity));
            trackEvents.Add(new NAudio.Midi.NoteEvent(endTick, midiChannel + 1, MidiCommandCode.NoteOff, noteEvent.Note.MidiNumber, 0));
        }
    }

    private static void AddPartEvents(
        List<MidiEvent> trackEvents,
        Pattern pattern,
        int channel,
        long patternStartTick,
        Song song,
        int midiChannel,
        MidiProgram basePatch,
        ChannelRoleAssignment? assignment,
        ArrangementSection? section,
        bool isDrumChannel)
    {
        if (!song.IsChannelAudible(channel))
            return;

        foreach (var part in pattern.Parts.Where(part => part.Channel == channel))
        {
            if (part.ProgramOverride != null && !isDrumChannel)
            {
                float overrideStartBeat = GetPartStartBeat(part);
                float overrideEndBeat = GetPartEndBeat(part);
                long overrideStartTick = patternStartTick + BeatsToTicks(overrideStartBeat);
                long overrideEndTick = patternStartTick + BeatsToTicks(overrideEndBeat);
                AddPatchEvents(trackEvents, overrideStartTick, midiChannel, part.ProgramOverride);
                if (!Equals(basePatch, part.ProgramOverride))
                    AddPatchEvents(trackEvents, overrideEndTick, midiChannel, basePatch);
            }

            foreach (var note in part.Notes)
            {
                long startTick = patternStartTick + BeatsToTicks(note.StartBeat);
                long endTick = patternStartTick + BeatsToTicks(note.EndBeat);
                int row = (int)Math.Round(note.StartBeat * song.RowsPerBeat);
                int velocity = ResolveVelocity(note.Velocity, note.Note.MidiNumber, row, song.RowsPerBeat, song.BeatsPerBar, part.IsDrumPart || isDrumChannel, assignment, section);

                trackEvents.Add(new NAudio.Midi.NoteEvent(startTick, midiChannel + 1, MidiCommandCode.NoteOn, note.Note.MidiNumber, velocity));
                trackEvents.Add(new NAudio.Midi.NoteEvent(endTick, midiChannel + 1, MidiCommandCode.NoteOff, note.Note.MidiNumber, 0));
            }

            foreach (var lane in part.AutomationLanes)
            {
                foreach (var point in lane.Points)
                {
                    long tick = patternStartTick + BeatsToTicks(point.Beat);
                    AddAutomationEvent(trackEvents, midiChannel, tick, lane.Type, point.Value);
                }
            }
        }
    }

    private static void AddPatchEvents(List<MidiEvent> trackEvents, long tick, int midiChannel, MidiProgram patch)
    {
        trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, MidiController.BankSelect, patch.BankMsb));
        trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, MidiController.BankSelectLsb, patch.BankLsb));
        trackEvents.Add(new PatchChangeEvent(tick, midiChannel + 1, patch.ProgramNumber));
    }

    private static void AddAutomationEvent(List<MidiEvent> trackEvents, int midiChannel, long tick, AutomationLaneType laneType, float value)
    {
        switch (laneType)
        {
            case AutomationLaneType.Expression:
                trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, ExpressionController, Math.Clamp((int)Math.Round(value), 0, 127)));
                break;
            case AutomationLaneType.Modulation:
                trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, MidiController.Modulation, Math.Clamp((int)Math.Round(value), 0, 127)));
                break;
            case AutomationLaneType.Sustain:
                trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, MidiController.Sustain, value >= 64f ? 127 : 0));
                break;
            case AutomationLaneType.ReverbSend:
                trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, ReverbSendController, Math.Clamp((int)Math.Round(value), 0, 127)));
                break;
            case AutomationLaneType.ChorusSend:
                trackEvents.Add(new ControlChangeEvent(tick, midiChannel + 1, ChorusSendController, Math.Clamp((int)Math.Round(value), 0, 127)));
                break;
            case AutomationLaneType.PitchBend:
                trackEvents.Add(new PitchWheelChangeEvent(tick, midiChannel + 1, Math.Clamp((int)Math.Round(value) + 8192, 0, 16383)));
                break;
        }
    }

    private static float GetPartStartBeat(Part part)
    {
        float noteStart = part.Notes.Count > 0 ? part.Notes.Min(note => note.StartBeat) : float.PositiveInfinity;
        float laneStart = part.AutomationLanes.SelectMany(lane => lane.Points).Select(point => point.Beat).DefaultIfEmpty(float.PositiveInfinity).Min();
        return float.IsPositiveInfinity(Math.Min(noteStart, laneStart)) ? 0f : Math.Min(noteStart, laneStart);
    }

    private static float GetPartEndBeat(Part part)
    {
        float noteEnd = part.Notes.Count > 0 ? part.Notes.Max(note => note.EndBeat) : 0f;
        float laneEnd = part.AutomationLanes.SelectMany(lane => lane.Points).Select(point => point.Beat).DefaultIfEmpty(0f).Max();
        return Math.Max(noteEnd, laneEnd);
    }

    private static bool IsDrumChannel(Song song, int channel)
    {
        if (song.DrumChannels.Contains(channel))
            return true;

        return song.Patterns.Any(pattern => pattern.Parts.Any(part => part.Channel == channel && part.IsDrumPart));
    }

    private static void AddExpressionEnvelope(
        List<MidiEvent> trackEvents,
        int midiChannel,
        Song song,
        SongProjectMetadata? metadata,
        ChannelRoleAssignment assignment,
        int ticksPerRow)
    {
        if (assignment.IsDrumChannel || metadata?.ArrangementPlan == null)
            return;

        int rowOffset = 0;
        byte? lastExpression = null;
        for (int index = 0; index < song.OrderList.Entries.Count; index++)
        {
            var entry = song.OrderList.Entries[index];
            var section = metadata.ArrangementPlan.Sections.ElementAtOrDefault(index);
            if (section != null)
            {
                byte expression = ResolveExpressionValue(section, assignment.Role, song.MasterVolume);
                if (lastExpression != expression)
                {
                    trackEvents.Add(new ControlChangeEvent((long)rowOffset * ticksPerRow, midiChannel + 1, ExpressionController, expression));
                    lastExpression = expression;
                }
            }

            if (entry.PatternIndex >= 0 && entry.PatternIndex < song.Patterns.Count)
                rowOffset += song.Patterns[entry.PatternIndex].RowCount;
        }
    }

    private static byte ResolveExpressionValue(ArrangementSection section, ChannelRole role, float masterVolume)
    {
        float roleScale = role switch
        {
            ChannelRole.Lead => 1.00f,
            ChannelRole.Bass => 0.94f,
            ChannelRole.Harmony => 0.88f,
            ChannelRole.PadLow => 0.80f,
            ChannelRole.PadHigh => 0.76f,
            _ => 0.92f
        };

        float expression = (0.56f + section.Intensity * 0.34f) * roleScale * Math.Clamp(masterVolume + 0.12f, 0.65f, 1.0f);
        return (byte)Math.Clamp((int)Math.Round(expression * 127), 48, 127);
    }

    private static int ResolveVelocity(
        int baseVelocity,
        int noteNumber,
        int row,
        int rowsPerBeat,
        int beatsPerBar,
        bool isDrumChannel,
        ChannelRoleAssignment? assignment,
        ArrangementSection? section)
    {
        float sectionBoost = 0.92f + (section?.Intensity ?? 0.62f) * 0.18f;
        float multiplier = sectionBoost;

        if (assignment != null)
        {
            multiplier *= assignment.Role switch
            {
                ChannelRole.Lead => 1.06f,
                ChannelRole.Bass => 1.00f,
                ChannelRole.Harmony => 0.94f,
                ChannelRole.PadLow => 0.88f,
                ChannelRole.PadHigh => 0.84f,
                _ => 1.0f
            };
        }

        multiplier *= isDrumChannel
            ? ResolveDrumAccent(noteNumber, row, rowsPerBeat, beatsPerBar)
            : ResolveBeatAccent(row, rowsPerBeat, beatsPerBar);

        return Math.Clamp((int)Math.Round(baseVelocity * multiplier), 1, 127);
    }

    private static float ResolveBeatAccent(int row, int rowsPerBeat, int beatsPerBar)
    {
        int rowsPerBar = rowsPerBeat * Math.Max(1, beatsPerBar);
        int rowInBar = row % rowsPerBar;
        if (rowInBar == 0)
            return 1.08f;
        if (rowInBar % rowsPerBeat == 0)
            return 1.03f;

        return 0.97f;
    }

    private static float ResolveDrumAccent(int noteNumber, int row, int rowsPerBeat, int beatsPerBar)
    {
        int rowsPerBar = rowsPerBeat * Math.Max(1, beatsPerBar);
        int rowInBar = row % rowsPerBar;
        bool barStart = rowInBar == 0;
        bool strongBeat = rowInBar == 0 || rowInBar == rowsPerBeat * Math.Min(2, Math.Max(0, beatsPerBar - 1));
        bool onBeat = rowInBar % rowsPerBeat == 0;
        int beat = rowInBar / rowsPerBeat;

        return noteNumber switch
        {
            49 or 57 => barStart ? 1.18f : 0.94f,
            35 or 36 => strongBeat ? 1.14f : onBeat ? 1.06f : 0.96f,
            38 or 40 => beat is 1 or 3 ? 1.12f : 0.98f,
            42 or 44 => onBeat ? 0.92f : 0.86f,
            46 => onBeat ? 0.98f : 0.92f,
            _ => onBeat ? 1.02f : 0.96f
        };
    }

    private static long BeatsToTicks(float beat) =>
        (long)Math.Round(beat * DeltaTicksPerQuarterNote);

    private static int GetEventPriority(MidiEvent midiEvent) =>
        midiEvent switch
        {
            NAudio.Midi.NoteEvent { CommandCode: MidiCommandCode.NoteOff } => 0,
            ControlChangeEvent => 1,
            PitchWheelChangeEvent => 1,
            PatchChangeEvent => 1,
            NAudio.Midi.NoteEvent { CommandCode: MidiCommandCode.NoteOn } => 2,
            _ => 3
        };
}
