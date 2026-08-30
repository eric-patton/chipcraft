using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Composition;

public class SongAnalyzer
{
    public SongAnalysis Analyze(Song song, SongProjectMetadata? metadata = null)
    {
        var assignments = ResolveAssignments(song, metadata);
        var key = ResolveSongKey(song, metadata);
        var arrangement = metadata?.ArrangementPlan;

        var loopQuality = AnalyzeLoopQuality(song, key, assignments);
        var phraseVariation = AnalyzePhraseVariation(song, assignments);
        var registerSeparation = AnalyzeRegisterSeparation(song, assignments);
        var rhythmicDensity = AnalyzeRhythmicDensity(song, assignments);
        var harmonicFit = AnalyzeHarmonicFit(song, key, arrangement, assignments);
        var melodyMemorability = AnalyzeMelodyMemorability(song, assignments);
        var sectionContrast = AnalyzeSectionContrast(song, arrangement, assignments);
        var cadenceStrength = AnalyzeCadenceStrength(song, key, arrangement, assignments);
        var channelCrowding = AnalyzeChannelCrowding(song, assignments);
        var roleCoverage = AnalyzeRoleCoverage(assignments);
        var exportReadiness = AnalyzeExportReadiness(song, assignments);

        var findings = new List<string>();
        var warnings = new List<string>();
        foreach (var metric in new[]
                 {
                     loopQuality, phraseVariation, registerSeparation, rhythmicDensity,
                     harmonicFit, melodyMemorability, sectionContrast,
                     cadenceStrength, channelCrowding, roleCoverage, exportReadiness
                 })
        {
            if (metric.Score < 0.55)
                findings.Add(metric.Summary);
            else if (metric.Score < 0.72)
                warnings.Add(metric.Summary);
        }

        return new SongAnalysis(
            loopQuality,
            phraseVariation,
            registerSeparation,
            rhythmicDensity,
            harmonicFit,
            melodyMemorability,
            sectionContrast,
            cadenceStrength,
            channelCrowding,
            roleCoverage,
            exportReadiness,
            findings,
            warnings);
    }

    public string Explain(Song song, SongProjectMetadata? metadata = null)
    {
        var analysis = metadata?.Analysis ?? Analyze(song, metadata);
        var arrangement = metadata?.ArrangementPlan;
        var assignments = ResolveAssignments(song, metadata);
        string resolvedKey = ResolveSongKey(song, metadata)?.ToString() ?? song.KeyName ?? "unknown key";
        string form = arrangement?.Form ?? $"{song.Patterns.Count} pattern cue";
        string cueType = DescribeCue(assignments);
        string strengths = string.Join("; ",
            new[]
            {
                analysis.LoopQuality,
                analysis.PhraseVariation,
                analysis.RegisterSeparation,
                analysis.RhythmicDensity,
                analysis.HarmonicFit,
                analysis.MelodyMemorability,
                analysis.SectionContrast,
                analysis.CadenceStrength,
                analysis.ChannelCrowding
            }
            .OrderByDescending(m => m.Score)
            .Take(2)
            .Select(m => m.Summary));

        string weakSpots = analysis.Findings.Count > 0
            ? string.Join("; ", analysis.Findings)
            : analysis.Warnings.Count > 0
                ? string.Join("; ", analysis.Warnings)
                : "No major weak spots were detected.";

        return $"{song.Title}: {cueType} {form} in {resolvedKey} at {song.Tempo} BPM ({song.BeatsPerBar}/{song.BeatUnit}). Strongest areas: {strengths}. Weak spots: {weakSpots}";
    }

    private static IReadOnlyList<ChannelRoleAssignment> ResolveAssignments(Song song, SongProjectMetadata? metadata)
    {
        if (metadata?.ChannelAssignments.Count > 0)
            return metadata.ChannelAssignments;

        return InferAssignments(song);
    }

    private static IReadOnlyList<ChannelRoleAssignment> InferAssignments(Song song)
    {
        var channels = Enumerable.Range(0, song.ChannelCount)
            .Select(channel =>
            {
                var noteEvents = GetSongEvents(song, channel).Where(e => !e.IsRest).ToList();
                song.ChannelPrograms.TryGetValue(channel, out var program);
                return new ChannelSnapshot(
                    channel,
                    noteEvents,
                    noteEvents.Count > 0 ? noteEvents.Average(e => e.Note.MidiNumber) : double.NaN,
                    program,
                    song.DrumChannels.Contains(channel),
                    channel < song.ChannelVolumes.Length ? song.ChannelVolumes[channel] : 0.75f,
                    channel < song.ChannelPans.Length ? song.ChannelPans[channel] : 0f);
            })
            .Where(channel => channel.IsDrumChannel || channel.NoteEvents.Count > 0)
            .ToList();

        if (channels.Count == 0)
            return [];

        var assignments = new List<ChannelRoleAssignment>();

        foreach (var drum in channels.Where(channel => channel.IsDrumChannel))
            assignments.Add(drum.ToAssignment(ChannelRole.Drums));

        var melodic = channels.Where(channel => !channel.IsDrumChannel).ToList();
        if (melodic.Count == 0)
            return assignments;

        ChannelSnapshot? bass = melodic
            .Where(channel => IsBassCategory(channel.Program))
            .OrderBy(channel => channel.AverageMidi)
            .FirstOrDefault();
        bass ??= melodic
            .Where(channel => channel.AverageMidi <= 55)
            .OrderBy(channel => channel.AverageMidi)
            .FirstOrDefault();

        ChannelSnapshot? lead = melodic
            .Where(channel => channel.Channel != bass?.Channel)
            .OrderByDescending(channel => IsLeadCategory(channel.Program))
            .ThenByDescending(channel => channel.AverageMidi)
            .FirstOrDefault();
        lead ??= melodic
            .Where(channel => channel.Channel != bass?.Channel)
            .OrderByDescending(channel => channel.AverageMidi)
            .FirstOrDefault();

        if (melodic.Count == 1)
            lead ??= melodic[0];

        if (bass == null && melodic.Count > 1)
        {
            bass = melodic
                .Where(channel => channel.Channel != lead?.Channel)
                .OrderBy(channel => channel.AverageMidi)
                .FirstOrDefault();
        }

        if (bass != null)
            assignments.Add(bass.ToAssignment(ChannelRole.Bass));
        if (lead != null)
            assignments.Add(lead.ToAssignment(ChannelRole.Lead));

        var remaining = melodic
            .Where(channel => channel.Channel != bass?.Channel && channel.Channel != lead?.Channel)
            .OrderBy(channel => channel.AverageMidi)
            .ToList();

        foreach (var channel in remaining)
            assignments.Add(channel.ToAssignment(InferSupportRole(channel)));

        return assignments;
    }

    private static ChannelRole InferSupportRole(ChannelSnapshot channel)
    {
        string category = channel.Program?.Category ?? "";
        string name = channel.Program?.Name ?? "";
        if (category.Equals("Synth Pad", StringComparison.OrdinalIgnoreCase))
            return channel.AverageMidi <= 66 ? ChannelRole.PadLow : ChannelRole.PadHigh;
        if (category.Equals("Ensemble", StringComparison.OrdinalIgnoreCase))
            return channel.AverageMidi <= 64 ? ChannelRole.PadLow : ChannelRole.PadHigh;
        if (category.Equals("Strings", StringComparison.OrdinalIgnoreCase) && name.Contains("Tremolo", StringComparison.OrdinalIgnoreCase))
            return channel.AverageMidi <= 64 ? ChannelRole.PadLow : ChannelRole.PadHigh;

        return channel.AverageMidi <= 60 ? ChannelRole.PadLow
            : channel.AverageMidi >= 72 ? ChannelRole.PadHigh
            : ChannelRole.Harmony;
    }

    private static bool IsBassCategory(MidiProgram? program)
    {
        if (program == null)
            return false;

        return program.Category.Equals("Bass", StringComparison.OrdinalIgnoreCase)
            || program.Name.Contains("Contrabass", StringComparison.OrdinalIgnoreCase)
            || program.Name.Contains("Cello", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLeadCategory(MidiProgram? program)
    {
        if (program == null)
            return false;

        return program.Category is "Piano" or "Guitar" or "Reed" or "Pipe" or "Brass" or "Synth Lead" or "Chromatic Percussion" or "Organ" or "Ethnic";
    }

    private static AnalysisMetric AnalyzeLoopQuality(
        Song song,
        Key? key,
        IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        if (!song.OrderList.LoopStartIndex.HasValue)
            return new AnalysisMetric("loopQuality", 0.65, "Cue is not currently configured to loop, so seam analysis is limited.");

        double score = song.OrderList.LoopStartIndex == 0 ? 0.58 : 0.46;
        var leadChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? 0;
        var bassChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Bass)?.Channel ?? 1;

        if (song.OrderList.Entries.Count > 0)
        {
            var lastPattern = song.Patterns[song.OrderList.Entries[^1].PatternIndex];
            var leadEvents = GetPatternEvents(lastPattern, leadChannel, song.RowsPerBeat).ToList();
            var bassEvents = GetPatternEvents(lastPattern, bassChannel, song.RowsPerBeat).ToList();

            if (key != null)
            {
                int tonic = key.Root.PitchClass;
                int dominant = (key.Root.PitchClass + 7) % 12;
                var lastLead = leadEvents.LastOrDefault(e => !e.IsRest);
                var lastBass = bassEvents.LastOrDefault(e => !e.IsRest);

                if (lastLead != null && (lastLead.Note.PitchClass == tonic || lastLead.Note.PitchClass == dominant))
                    score += 0.20;
                if (lastBass != null && lastBass.Note.PitchClass == tonic)
                    score += 0.15;
            }

            var drumChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Drums)?.Channel ?? -1;
            if (drumChannel >= 0)
            {
                int finalBarRows = song.RowsPerBeat * song.BeatsPerBar;
                int finalBarStartRow = Math.Max(0, lastPattern.RowCount - finalBarRows);
                int finalBarHits = CountStarts(lastPattern, drumChannel, finalBarStartRow, lastPattern.RowCount, song.RowsPerBeat);
                if (finalBarHits >= 6)
                    score += 0.10;
            }
        }

        return new AnalysisMetric("loopQuality", Math.Clamp(score, 0.0, 1.0),
            score >= 0.72
                ? "Loop cadence and seam feel stable."
                : "Loop seam could resolve more cleanly into the restart.");
    }

    private static AnalysisMetric AnalyzePhraseVariation(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        if (song.OrderList.Entries.Count <= 1)
            return new AnalysisMetric("phraseVariation", 0.50, "Single-pattern song leaves little room for phrase variation.");

        var leadChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? 0;
        var signatures = song.OrderList.Entries
            .Select(e => BuildPatternSignature(song.Patterns[e.PatternIndex], leadChannel, song.RowsPerBeat))
            .ToList();
        double distinctRatio = signatures.Distinct().Count() / (double)signatures.Count;
        double score = 0.35 + distinctRatio * 0.65;

        return new AnalysisMetric("phraseVariation", Math.Clamp(score, 0.0, 1.0),
            score >= 0.72
                ? "Repeated phrases vary enough to keep the loop moving."
                : "Repeated phrases are still too close to exact copies.");
    }

    private static AnalysisMetric AnalyzeRegisterSeparation(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        var melodicAssignments = assignments.Where(a => !a.IsDrumChannel).ToList();
        var channelAverages = melodicAssignments
            .Select(a =>
            {
                var notes = GetSongEvents(song, a.Channel).Where(e => !e.IsRest).Select(e => e.Note.MidiNumber).ToList();
                return (Assignment: a, Average: notes.Count > 0 ? notes.Average() : double.NaN);
            })
            .Where(x => !double.IsNaN(x.Average))
            .OrderBy(x => x.Average)
            .ToList();

        if (channelAverages.Count < 2)
            return new AnalysisMetric("registerSeparation", 0.72, "Sparse scoring leaves little register overlap to resolve.");
        if (channelAverages.Count < 3)
            return new AnalysisMetric("registerSeparation", 0.62, "A small number of melodic layers limits how much register spread can be judged.");

        double minGap = double.MaxValue;
        for (int i = 1; i < channelAverages.Count; i++)
            minGap = Math.Min(minGap, channelAverages[i].Average - channelAverages[i - 1].Average);

        double score = Math.Clamp(minGap / 10.0, 0.0, 1.0);
        return new AnalysisMetric("registerSeparation", score,
            score >= 0.72
                ? "Bass, support, and lead parts occupy distinct ranges."
                : "Some layers are crowding each other in the same register.");
    }

    private static AnalysisMetric AnalyzeRhythmicDensity(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        int rowsPerBar = Math.Max(1, song.RowsPerBeat * song.BeatsPerBar);
        int totalBars = Math.Max(1, song.TotalRows / rowsPerBar);
        int totalStarts = assignments.Sum(assignment =>
            song.Patterns.Sum(pattern => CountStarts(pattern, assignment.Channel, 0, pattern.RowCount, song.RowsPerBeat)));

        double startsPerBar = totalStarts / (double)totalBars;
        double score = startsPerBar switch
        {
            < 4 => 0.35,
            < 7 => 0.62,
            <= 18 => 0.88,
            <= 24 => 0.72,
            _ => 0.45
        };

        return new AnalysisMetric("rhythmicDensity", score,
            score >= 0.72
                ? "Rhythmic density is in a healthy range for a looped cue."
                : "Rhythmic density is either too sparse or too crowded.");
    }

    private static AnalysisMetric AnalyzeHarmonicFit(
        Song song,
        Key? key,
        ArrangementPlan? arrangement,
        IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        if (key == null)
            return new AnalysisMetric("harmonicFit", 0.60, "Harmonic fit is limited without a declared or inferable key center.");

        int total = 0;
        int inScale = 0;
        int strongBeatNotes = 0;
        int strongBeatChordTones = 0;

        for (int orderIndex = 0; orderIndex < song.OrderList.Entries.Count; orderIndex++)
        {
            var pattern = song.Patterns[song.OrderList.Entries[orderIndex].PatternIndex];
            var section = arrangement?.Sections.ElementAtOrDefault(orderIndex);

            foreach (var assignment in assignments.Where(a => !a.IsDrumChannel))
            {
                foreach (var evt in GetPatternEvents(pattern, assignment.Channel, song.RowsPerBeat).Where(e => !e.IsRest))
                {
                    total++;
                    if (key.Scale.Contains(evt.Note))
                        inScale++;

                    if (section != null)
                    {
                        int bar = Math.Clamp((int)(evt.StartBeat / song.BeatsPerBar), 0, section.Chords.Length - 1);
                        bool isStrongBeat = Math.Abs(evt.StartBeat % song.BeatsPerBar) < 0.01f
                            || Math.Abs(evt.StartBeat % song.BeatsPerBar - Math.Max(1f, song.BeatsPerBar / 2f)) < 0.01f;
                        if (isStrongBeat)
                        {
                            strongBeatNotes++;
                            var chord = Theory.Chord.Parse(section.Chords[bar]);
                            if (chord.GetNotes().Any(n => n.PitchClass == evt.Note.PitchClass))
                                strongBeatChordTones++;
                        }
                    }
                }
            }
        }

        if (total == 0)
            return new AnalysisMetric("harmonicFit", 0.20, "Melodic and harmonic channels are empty.");

        double scaleFit = inScale / (double)total;
        double chordFit = strongBeatNotes > 0 ? strongBeatChordTones / (double)strongBeatNotes : scaleFit;
        double score = Math.Clamp(scaleFit * 0.55 + chordFit * 0.45, 0.0, 1.0);

        return new AnalysisMetric("harmonicFit", score,
            score >= 0.72
                ? "Most melodic notes fit the declared or inferred key and land cleanly against strong beats."
                : "Too many melodic notes miss the key center or clash with strong-beat harmony.");
    }

    private static AnalysisMetric AnalyzeMelodyMemorability(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        int leadChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? -1;
        if (leadChannel < 0)
            return new AnalysisMetric("melodyMemorability", 0.35, "No lead channel is available to judge melodic memorability.");

        var notes = GetSongEvents(song, leadChannel)
            .Where(e => !e.IsRest)
            .Select(e => e.Note.PitchClass)
            .ToList();

        if (notes.Count < 6)
            return new AnalysisMetric("melodyMemorability", 0.45, "Lead line is too short to establish a memorable motif.");

        var ngrams = Enumerable.Range(0, notes.Count - 2)
            .Select(i => $"{notes[i]}-{notes[i + 1]}-{notes[i + 2]}")
            .ToList();
        if (ngrams.Count == 0)
            return new AnalysisMetric("melodyMemorability", 0.45, "Lead line is too short to establish a memorable motif.");

        var repeated = ngrams.GroupBy(x => x).Where(g => g.Count() > 1).Sum(g => g.Count());
        double repeatedShare = repeated / (double)ngrams.Count;
        double uniqueness = ngrams.Distinct().Count() / (double)ngrams.Count;
        double balance = 1.0 - Math.Min(1.0, Math.Abs(uniqueness - 0.72) / 0.72);
        double score = Math.Clamp(0.25 + repeatedShare * 0.45 + balance * 0.30, 0.0, 1.0);

        return new AnalysisMetric("melodyMemorability", score,
            score >= 0.72
                ? "Lead line repeats a recognizable motif without becoming static."
                : "Lead line lacks a strong recurring motif or varies too randomly.");
    }

    private static AnalysisMetric AnalyzeSectionContrast(
        Song song,
        ArrangementPlan? arrangement,
        IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        if (arrangement == null || arrangement.Sections.Count < 2)
            return new AnalysisMetric("sectionContrast", 0.60, "The cue has too few sections to judge contrast strongly.");

        int leadChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? 0;
        string Normalize(string label) => label.TrimEnd('\'');

        var grouped = arrangement.Sections
            .Select((section, index) => (section, pattern: song.Patterns[song.OrderList.Entries[index].PatternIndex]))
            .GroupBy(x => Normalize(x.section.Label))
            .ToDictionary(
                g => g.Key,
                g => string.Join("|", g.SelectMany(x => GetPatternEvents(x.pattern, leadChannel, song.RowsPerBeat)
                    .Where(e => !e.IsRest)
                    .Select(e => $"{e.StartBeat:0.##}:{e.Note.PitchClass}"))));

        if (grouped.Count < 2)
            return new AnalysisMetric("sectionContrast", 0.62, "The cue mostly develops one idea rather than contrasting sections.");

        var labels = grouped.Keys.ToList();
        double totalDistance = 0;
        int pairs = 0;
        for (int i = 0; i < labels.Count; i++)
        {
            for (int j = i + 1; j < labels.Count; j++)
            {
                pairs++;
                totalDistance += StringDistance(grouped[labels[i]], grouped[labels[j]]);
            }
        }

        double score = pairs > 0 ? Math.Clamp(totalDistance / pairs, 0.0, 1.0) : 0.60;
        return new AnalysisMetric("sectionContrast", score,
            score >= 0.72
                ? "Contrasting sections bring enough lift and release inside the loop."
                : "Sections are still too similar to each other.");
    }

    private static AnalysisMetric AnalyzeCadenceStrength(
        Song song,
        Key? key,
        ArrangementPlan? arrangement,
        IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        if (key == null)
            return new AnalysisMetric("cadenceStrength", 0.55, "Cadence strength is limited without a declared or inferable key center.");

        int leadChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? -1;
        int bassChannel = assignments.FirstOrDefault(a => a.Role == ChannelRole.Bass)?.Channel ?? -1;
        if (leadChannel < 0 && bassChannel < 0)
            return new AnalysisMetric("cadenceStrength", 0.55, "No stable melodic anchor is available to judge cadence.");

        if (arrangement == null || arrangement.Sections.Count == 0)
            return AnalyzeCadenceWithoutSections(song, key, leadChannel, bassChannel);

        double totalScore = 0;
        foreach (var (section, index) in arrangement.Sections.Select((section, index) => (section, index)))
        {
            var pattern = song.Patterns[song.OrderList.Entries[index].PatternIndex];
            var lead = leadChannel >= 0 ? GetPatternEvents(pattern, leadChannel, song.RowsPerBeat).LastOrDefault(e => !e.IsRest) : null;
            var bass = bassChannel >= 0 ? GetPatternEvents(pattern, bassChannel, song.RowsPerBeat).LastOrDefault(e => !e.IsRest) : null;
            var finalChord = Theory.Chord.Parse(section.Chords[^1]);

            double sectionScore = 0.2;
            if (lead != null && (lead.Note.PitchClass == finalChord.Root.PitchClass || lead.Note.PitchClass == key.Root.PitchClass))
                sectionScore += 0.35;
            if (bass != null && (bass.Note.PitchClass == finalChord.Root.PitchClass || bass.Note.PitchClass == key.Root.PitchClass))
                sectionScore += 0.35;
            if (lead != null && bass != null && lead.Note.PitchClass != bass.Note.PitchClass)
                sectionScore += 0.10;

            totalScore += Math.Min(1.0, sectionScore);
        }

        double score = Math.Clamp(totalScore / arrangement.Sections.Count, 0.0, 1.0);
        return new AnalysisMetric("cadenceStrength", score,
            score >= 0.72
                ? "Section endings resolve with enough cadence to support the loop."
                : "Section endings need clearer harmonic or bass resolution.");
    }

    private static AnalysisMetric AnalyzeChannelCrowding(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        var melodicChannels = assignments.Where(a => !a.IsDrumChannel).Select(a => a.Channel).ToList();
        if (melodicChannels.Count < 2)
            return new AnalysisMetric("channelCrowding", 0.80, "There are too few melodic channels to create significant crowding.");

        int closePairs = 0;
        int totalPairs = 0;
        foreach (var pattern in song.Patterns)
        {
            for (int row = 0; row < pattern.RowCount; row++)
            {
                var rowNotes = melodicChannels
                    .Select(channel => channel < pattern.ChannelCount ? pattern.GetCell(row, channel).Note : null)
                    .Where(note => note.HasValue && !note.Value.IsRest && !note.Value.IsCut)
                    .Select(note => note!.Value.MidiNumber)
                    .ToList();

                for (int i = 0; i < rowNotes.Count; i++)
                {
                    for (int j = i + 1; j < rowNotes.Count; j++)
                    {
                        totalPairs++;
                        if (Math.Abs(rowNotes[i] - rowNotes[j]) <= 3)
                            closePairs++;
                    }
                }
            }
        }

        if (totalPairs == 0)
            return new AnalysisMetric("channelCrowding", 0.80, "Channels are spaced well enough that direct crowding is limited.");

        double crowdRatio = closePairs / (double)totalPairs;
        double score = Math.Clamp(1.0 - crowdRatio * 2.0, 0.0, 1.0);
        return new AnalysisMetric("channelCrowding", score,
            score >= 0.72
                ? "Channel voicings avoid excessive note collisions."
                : "Too many channels stack close notes at the same moment.");
    }

    private static AnalysisMetric AnalyzeRoleCoverage(IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        bool hasDrums = assignments.Any(a => a.Role == ChannelRole.Drums);
        int melodicChannels = assignments.Count(a => !a.IsDrumChannel);
        var required = hasDrums
            ? new[] { ChannelRole.Lead, ChannelRole.Bass, ChannelRole.Drums }
            : melodicChannels <= 1
                ? new[] { ChannelRole.Lead }
                : melodicChannels == 2
                    ? new[] { ChannelRole.Lead, ChannelRole.Bass }
                    : new[] { ChannelRole.Lead, ChannelRole.Bass, ChannelRole.Harmony };
        int presentRequired = required.Count(role =>
            role == ChannelRole.Harmony
                ? assignments.Any(a => a.Role is ChannelRole.Harmony or ChannelRole.PadLow or ChannelRole.PadHigh)
                : assignments.Any(a => a.Role == role));
        bool hasSupport = assignments.Any(a => a.Role is ChannelRole.Harmony or ChannelRole.PadLow or ChannelRole.PadHigh);
        double score = presentRequired / (double)required.Length;
        if (hasSupport)
            score = Math.Min(1.0, score + 0.2);

        return new AnalysisMetric("roleCoverage", Math.Clamp(score, 0.0, 1.0),
            score >= 0.80
                ? hasDrums
                    ? "Lead, bass, drums, and support roles are covered cleanly."
                    : "The cue covers the core roles expected for its sparse instrumentation."
                : hasDrums
                    ? "The arrangement is missing one or more core full-band roles."
                    : "The cue is still missing one or more core melodic/support roles.");
    }

    private static AnalysisMetric AnalyzeExportReadiness(Song song, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        double score = 0;
        if (song.Patterns.Count > 0 && song.OrderList.Entries.Count > 0)
            score += 0.35;
        if (assignments.Count > 0)
            score += 0.25;
        if (song.ChannelPrograms.Count >= Math.Max(1, assignments.Count(a => !a.IsDrumChannel)))
            score += 0.20;
        if (!string.IsNullOrWhiteSpace(song.KeyName))
            score += 0.10;
        if (song.BeatsPerBar > 0 && song.BeatUnit > 0)
            score += 0.10;
        if (song.TotalRows > 0 && song.TotalDurationSeconds > 0)
            score += 0.10;

        return new AnalysisMetric("exportReadiness", Math.Clamp(score, 0.0, 1.0),
            score >= 0.80
                ? "Song state is complete enough to export cleanly."
                : "Song state is still missing metadata, programs, or arranged content.");
    }

    private static string BuildPatternSignature(Pattern pattern, int channel, int rowsPerBeat)
    {
        var seq = GetPatternEvents(pattern, channel, rowsPerBeat)
            .Where(e => !e.IsRest)
            .Select(e => $"{e.StartBeat:0.##}:{e.DurationBeats:0.##}:{e.Note.PitchClass}")
            .ToArray();
        return string.Join("|", seq);
    }

    private static IEnumerable<NoteEvent> GetPatternEvents(Pattern pattern, int channel, int rowsPerBeat = 4)
    {
        var events = new List<NoteEvent>();
        if (channel >= 0 && channel < pattern.ChannelCount)
            events.AddRange(pattern.ToNoteSequence(channel, rowsPerBeat).Events);

        events.AddRange(pattern.Parts
            .Where(part => part.Channel == channel)
            .SelectMany(part => part.Notes)
            .Select(note => new NoteEvent(note.Note, note.StartBeat, note.DurationBeats, note.Velocity / 127f)));

        return events.OrderBy(evt => evt.StartBeat).ThenBy(evt => evt.Note.MidiNumber).ToList();
    }

    private static double StringDistance(string left, string right)
    {
        if (left == right)
            return 0;
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return 1;

        var leftSet = left.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var rightSet = right.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        int intersection = leftSet.Intersect(rightSet).Count();
        int union = leftSet.Union(rightSet).Count();
        if (union == 0)
            return 0;

        return 1.0 - intersection / (double)union;
    }

    private static IEnumerable<NoteEvent> GetSongEvents(Song song, int channel)
    {
        foreach (var entry in song.OrderList.Entries)
        {
            if (entry.PatternIndex < 0 || entry.PatternIndex >= song.Patterns.Count)
                continue;

            foreach (var evt in GetPatternEvents(song.Patterns[entry.PatternIndex], channel, song.RowsPerBeat))
                yield return evt;
        }
    }

    private static int CountStarts(Pattern pattern, int channel, int startRow, int endRow, int rowsPerBeat)
    {
        int count = 0;
        if (channel >= 0 && channel < pattern.ChannelCount)
        {
            for (int row = startRow; row < endRow; row++)
            {
                var cell = pattern.GetCell(row, channel);
                if (cell.Note.HasValue && !cell.Note.Value.IsRest && !cell.Note.Value.IsCut)
                    count++;
            }
        }

        count += pattern.Parts
            .Where(part => part.Channel == channel)
            .SelectMany(part => part.Notes)
            .Count(note =>
            {
                int row = (int)Math.Round(note.StartBeat * rowsPerBeat);
                return row >= startRow && row < endRow;
            });

        return count;
    }

    private static Key? ResolveSongKey(Song song, SongProjectMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Spec?.KeyName))
            return Key.Parse(metadata.Spec.KeyName);
        if (!string.IsNullOrWhiteSpace(song.KeyName))
            return Key.Parse(song.KeyName);

        return InferKey(song);
    }

    private static Key? InferKey(Song song)
    {
        var notes = Enumerable.Range(0, song.ChannelCount)
            .Where(channel => !song.DrumChannels.Contains(channel))
            .SelectMany(channel => GetSongEvents(song, channel))
            .Where(evt => !evt.IsRest)
            .Select(evt => evt.Note)
            .ToList();
        if (notes.Count == 0)
            return null;

        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        Key? bestKey = null;
        double bestScore = double.NegativeInfinity;
        foreach (string root in names)
        {
            foreach (string candidate in new[] { root, $"{root}m" })
            {
                var key = Key.Parse(candidate);
                double score = notes.Count(note => key.Scale.Contains(note)) / (double)notes.Count;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestKey = key;
                }
            }
        }

        return bestKey;
    }

    private static string DescribeCue(IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        bool hasDrums = assignments.Any(a => a.Role == ChannelRole.Drums);
        int melodicChannels = assignments.Count(a => !a.IsDrumChannel);
        if (!hasDrums && melodicChannels <= 1)
            return "solo cue";
        if (!hasDrums && melodicChannels <= 3)
            return "sparse cue";
        if (!hasDrums)
            return "melodic cue";
        if (melodicChannels <= 2)
            return "rhythmic cue";
        return "full cue";
    }

    private static AnalysisMetric AnalyzeCadenceWithoutSections(Song song, Key key, int leadChannel, int bassChannel)
    {
        var lastPattern = song.OrderList.Entries.Count > 0
            ? song.Patterns[song.OrderList.Entries[^1].PatternIndex]
            : null;
        if (lastPattern == null)
            return new AnalysisMetric("cadenceStrength", 0.45, "Cadence cannot be judged because the cue is empty.");

        var lead = leadChannel >= 0 ? GetPatternEvents(lastPattern, leadChannel, song.RowsPerBeat).LastOrDefault(evt => !evt.IsRest) : null;
        var bass = bassChannel >= 0 ? GetPatternEvents(lastPattern, bassChannel, song.RowsPerBeat).LastOrDefault(evt => !evt.IsRest) : null;
        int tonic = key.Root.PitchClass;
        int dominant = (key.Root.PitchClass + 7) % 12;
        double score = 0.28;
        if (lead != null && (lead.Note.PitchClass == tonic || lead.Note.PitchClass == dominant))
            score += 0.32;
        if (bass != null && bass.Note.PitchClass == tonic)
            score += 0.28;
        if (lead != null && bass != null && lead.Note.PitchClass != bass.Note.PitchClass)
            score += 0.08;

        return new AnalysisMetric("cadenceStrength", Math.Clamp(score, 0.0, 1.0),
            score >= 0.72
                ? "The cue lands on a stable cadence against its key center."
                : "Ending notes do not yet resolve strongly against the key center.");
    }

    private sealed record ChannelSnapshot(
        int Channel,
        IReadOnlyList<NoteEvent> NoteEvents,
        double AverageMidi,
        MidiProgram? Program,
        bool IsDrumChannel,
        float Volume,
        float Pan)
    {
        public ChannelRoleAssignment ToAssignment(ChannelRole role)
        {
            string programName = IsDrumChannel
                ? MidiProgram.Drums.Name
                : Program?.Name ?? "Unknown";

            return new ChannelRoleAssignment(Channel, role, programName, Volume, Pan, IsDrumChannel);
        }
    }
}
