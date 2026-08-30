using System.Text.RegularExpressions;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Engine.Composition;

public class SongRevisionEngine
{
    public SongCompositionResult Revise(
        Song song,
        SongProjectMetadata metadata,
        string? prompt = null,
        Mood? mood = null,
        float? energy = null,
        string? palette = null,
        int? bars = null,
        int? seed = null,
        string? form = null,
        string? sectionLabel = null)
    {
        var spec = metadata.Spec ?? throw new InvalidOperationException("Cannot revise a song without composition metadata.");
        string lower = prompt?.ToLowerInvariant() ?? "";
        var targetSections = ResolveTargetSections(metadata, sectionLabel, prompt);

        if ((mood.HasValue || bars.HasValue || !string.IsNullOrWhiteSpace(form)) && targetSections.Count == 0)
        {
            var recomposedSpec = spec with
            {
                Mood = mood ?? spec.Mood,
                Bars = bars ?? spec.Bars,
                Energy = energy ?? spec.Energy,
                Palette = !string.IsNullOrWhiteSpace(palette) ? palette : spec.Palette,
                Seed = seed ?? spec.Seed,
                Prompt = string.IsNullOrWhiteSpace(prompt) ? spec.Prompt : prompt!,
                FormHint = !string.IsNullOrWhiteSpace(form) ? form! : spec.FormHint
            };

            return new SongComposer().Compose(recomposedSpec);
        }

        var updatedMetadata = metadata;

        if (!string.IsNullOrWhiteSpace(palette) || lower.Contains("palette") || lower.Contains("lighter") || lower.Contains("brighter") || lower.Contains("darker"))
        {
            string nextPalette = !string.IsNullOrWhiteSpace(palette)
                ? palette!
                : lower.Contains("lighter") || lower.Contains("brighter")
                    ? "bright"
                    : "dark";

            updatedMetadata = ReassignPalette(song, updatedMetadata, spec with { Palette = nextPalette });
        }

        if (targetSections.Count > 0 || lower.Contains("rewrite") || lower.Contains("regenerate"))
        {
            var rewriteTargets = targetSections.Count > 0
                ? targetSections
                : Enumerable.Range(0, song.OrderList.Entries.Count).ToHashSet();
            RewriteSections(song, updatedMetadata, rewriteTargets, prompt, seed ?? spec.Seed);
        }

        if (lower.Contains("drum") || lower.Contains("percussion") || lower.Contains("busier") || lower.Contains("simpler"))
        {
            float delta = lower.Contains("simpler") ? -0.15f : 0.15f;
            float revisedEnergy = Math.Clamp(energy ?? spec.Energy + delta, 0.2f, 0.95f);
            RegenerateDrums(song, updatedMetadata, revisedEnergy, seed ?? spec.Seed, targetSections);
            updatedMetadata = updatedMetadata with
            {
                Spec = spec with { Energy = revisedEnergy, Seed = seed ?? spec.Seed }
            };
        }

        if (lower.Contains("repeat") || lower.Contains("variation") || lower.Contains("less repetitive"))
            IncreaseVariation(song, updatedMetadata, seed ?? spec.Seed, targetSections);

        if (lower.Contains("loop seam") || lower.Contains("seamless") || lower.Contains("ending") || lower.Contains("transition"))
            StrengthenLoopSeam(song, updatedMetadata, targetSections);

        var analyzer = new SongAnalyzer();
        var analysis = analyzer.Analyze(song, updatedMetadata);
        updatedMetadata = updatedMetadata with
        {
            Analysis = analysis,
            Warnings = updatedMetadata.WarningList.Concat(analysis.Warnings).Distinct().ToArray()
        };

        return new SongCompositionResult(song, updatedMetadata);
    }

    private static SongProjectMetadata ReassignPalette(Song song, SongProjectMetadata metadata, CompositionSpec spec)
    {
        var palette = PaletteProfileLibrary.Resolve(spec.Palette, spec.Mood, spec.Genre);

        foreach (var assignment in palette.Assignments)
        {
            if (assignment.IsDrumChannel)
            {
                song.SetDrumChannel(assignment.Channel);
            }
            else
            {
                var program = GeneralMidi.FindByName(assignment.ProgramName) ?? GeneralMidi.GetProgram(0);
                song.SetChannelProgram(assignment.Channel, program);
            }

            if (assignment.Channel < song.ChannelVolumes.Length)
                song.ChannelVolumes[assignment.Channel] = assignment.Volume;
            if (assignment.Channel < song.ChannelPans.Length)
                song.ChannelPans[assignment.Channel] = assignment.Pan;
        }

        return metadata with
        {
            Spec = spec,
            ChannelAssignments = palette.Assignments
        };
    }

    private static void RegenerateDrums(Song song, SongProjectMetadata metadata, float energy, int? seed, IReadOnlySet<int> targetSections)
    {
        var drumChannel = metadata.ChannelAssignments.FirstOrDefault(a => a.Role == ChannelRole.Drums)?.Channel ?? -1;
        if (drumChannel < 0 || metadata.ArrangementPlan == null || metadata.Spec == null)
            return;

        var style = SongComposer.ResolveDrumStyle(metadata.Spec.Genre, metadata.Spec.Mood);

        for (int index = 0; index < metadata.ArrangementPlan.Sections.Count; index++)
        {
            if (targetSections.Count > 0 && !targetSections.Contains(index))
                continue;

            var section = metadata.ArrangementPlan.Sections[index];
            var pattern = song.Patterns[song.OrderList.Entries[index].PatternIndex];
            ClearChannel(pattern, drumChannel);

            int sectionEnergy = (int)Math.Clamp(Math.Round((energy * 7) + section.Intensity * 2), 2, 10);
            var generator = new DrumPatternGenerator(seed.HasValue ? seed.Value + index * 37 : null);
            var drums = generator.Generate(new DrumPatternOptions(style, sectionEnergy, section.Bars, Fills: true));
            pattern.ApplyDrumPattern(drums, drumChannel, metadata.Spec.RowsPerBeat);
        }
    }

    private static void IncreaseVariation(Song song, SongProjectMetadata metadata, int? seed, IReadOnlySet<int> targetSections)
    {
        if (metadata.ArrangementPlan == null || metadata.Spec == null)
            return;

        int leadChannel = metadata.ChannelAssignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? -1;
        int harmonyChannel = metadata.ChannelAssignments.FirstOrDefault(a => a.Role == ChannelRole.Harmony)?.Channel ?? -1;
        if (leadChannel < 0)
            return;

        for (int index = 0; index < metadata.ArrangementPlan.Sections.Count; index++)
        {
            var section = metadata.ArrangementPlan.Sections[index];
            if (string.IsNullOrEmpty(section.VariationOf))
                continue;
            if (targetSections.Count > 0 && !targetSections.Contains(index))
                continue;

            var pattern = song.Patterns[song.OrderList.Entries[index].PatternIndex];
            var lead = pattern.ToNoteSequence(leadChannel, metadata.Spec.RowsPerBeat);
            var variedLead = SongComposer.VarySequence(
                lead,
                metadata.Spec.ToKey(),
                section.Intensity,
                seed.HasValue ? seed.Value + 401 + index : null,
                aggressive: true);

            ClearChannel(pattern, leadChannel);
            pattern.ApplyNoteSequence(variedLead, leadChannel, rowsPerBeat: metadata.Spec.RowsPerBeat);

            if (harmonyChannel >= 0)
            {
                var progression = SongComposer.ProgressionFromChords(metadata.Spec.ToKey(), section.Chords);
                var harmony = new HarmonyGenerator(seed.HasValue ? seed.Value + 557 + index : null)
                    .Generate(new HarmonyOptions(metadata.Spec.ToKey(), variedLead, progression, HarmonyStyle.ThirdsBelow));
                ClearChannel(pattern, harmonyChannel);
                pattern.ApplyNoteSequence(harmony, harmonyChannel, rowsPerBeat: metadata.Spec.RowsPerBeat);
            }
        }
    }

    private static void StrengthenLoopSeam(Song song, SongProjectMetadata metadata, IReadOnlySet<int> targetSections)
    {
        if (metadata.ArrangementPlan == null || metadata.Spec == null || song.OrderList.Entries.Count == 0)
            return;

        var key = metadata.Spec.ToKey();
        int leadChannel = metadata.ChannelAssignments.FirstOrDefault(a => a.Role == ChannelRole.Lead)?.Channel ?? -1;
        int bassChannel = metadata.ChannelAssignments.FirstOrDefault(a => a.Role == ChannelRole.Bass)?.Channel ?? -1;

        IEnumerable<int> indices = targetSections.Count > 0
            ? targetSections.OrderBy(i => i)
            : new[] { song.OrderList.Entries.Count - 1 };

        foreach (int index in indices)
        {
            if (index < 0 || index >= song.OrderList.Entries.Count)
                continue;

            var pattern = song.Patterns[song.OrderList.Entries[index].PatternIndex];
            if (leadChannel >= 0)
                SetFinalResolution(pattern, leadChannel, key.Scale.GetDegree(1, 5));
            if (bassChannel >= 0)
                SetFinalResolution(pattern, bassChannel, key.Scale.GetDegree(1, 2));
        }
    }

    private static void RewriteSections(Song song, SongProjectMetadata metadata, IReadOnlySet<int> targetSections, string? prompt, int? seed)
    {
        if (metadata.ArrangementPlan == null || metadata.Spec == null)
            return;

        var baseSpec = metadata.Spec;
        var palette = PaletteProfileLibrary.Resolve(baseSpec.Palette, baseSpec.Mood, baseSpec.Genre);
        float promptEnergyDelta = (prompt?.ToLowerInvariant().Contains("bigger") ?? false) || (prompt?.ToLowerInvariant().Contains("busier") ?? false)
            ? 0.08f
            : (prompt?.ToLowerInvariant().Contains("softer") ?? false) || (prompt?.ToLowerInvariant().Contains("lighter") ?? false)
                ? -0.08f
                : 0f;

        foreach (int index in targetSections)
        {
            if (index < 0 || index >= metadata.ArrangementPlan.Sections.Count)
                continue;

            var section = metadata.ArrangementPlan.Sections[index];
            var sectionSpec = baseSpec with
            {
                Energy = Math.Clamp(baseSpec.Energy + promptEnergyDelta + 0.02f * index, 0.2f, 0.95f),
                Seed = seed.HasValue ? seed.Value + 2000 + index * 53 : baseSpec.Seed
            };

            var replacement = SongComposer.ComposeSectionPattern(sectionSpec, section, palette, index + 100);
            int patternIndex = song.OrderList.Entries[index].PatternIndex;
            replacement.Id = song.Patterns[patternIndex].Id;
            song.Patterns[patternIndex] = replacement;
        }
    }

    private static HashSet<int> ResolveTargetSections(SongProjectMetadata metadata, string? explicitSectionLabel, string? prompt)
    {
        var targets = new HashSet<int>();
        if (metadata.ArrangementPlan == null)
            return targets;

        string? label = explicitSectionLabel;
        if (string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(prompt))
        {
            var match = Regex.Match(prompt, @"(?:rewrite|regenerate|rework)\s+([A-Z](?:'+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
                label = match.Groups[1].Value;
        }

        if (string.IsNullOrWhiteSpace(label))
            return targets;

        string normalized = label.Trim();
        foreach (var (section, index) in metadata.ArrangementPlan.Sections.Select((section, index) => (section, index)))
        {
            if (section.Label.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                targets.Add(index);
        }

        return targets;
    }

    private static void SetFinalResolution(Pattern pattern, int channel, Note note)
    {
        int startRow = Math.Max(0, pattern.RowCount - 8);
        for (int row = pattern.RowCount - 1; row >= startRow; row--)
        {
            var cell = pattern.GetCell(row, channel);
            if (cell.Note.HasValue && !cell.Note.Value.IsRest && !cell.Note.Value.IsCut)
            {
                pattern.SetCell(row, channel, cell with { Note = note, Volume = 13 });
                int cutRow = Math.Min(pattern.RowCount - 1, row + 4);
                if (cutRow > row)
                    pattern.SetCell(cutRow, channel, new PatternCell(Note.Cut));
                break;
            }
        }
    }

    private static void ClearChannel(Pattern pattern, int channel)
    {
        for (int row = 0; row < pattern.RowCount; row++)
            pattern.ClearCell(row, channel);
    }
}
