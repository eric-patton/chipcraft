using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Composition;

public class SongComposer
{
    private readonly SongAnalyzer _analyzer;
    private readonly SongRevisionEngine _revisionEngine;

    public SongComposer(SongAnalyzer? analyzer = null, SongRevisionEngine? revisionEngine = null)
    {
        _analyzer = analyzer ?? new SongAnalyzer();
        _revisionEngine = revisionEngine ?? new SongRevisionEngine();
    }

    public CompositionSpec ResolveSpec(
        string title,
        string prompt,
        Genre genre,
        Mood mood,
        int? bars = null,
        bool? loop = null,
        string? key = null,
        string? scaleType = null,
        int? tempo = null,
        string? palette = null,
        int? seed = null,
        string? form = null)
    {
        int resolvedBars = bars ?? 16;
        resolvedBars = Math.Clamp(((resolvedBars + 1) / 4) * 4, 8, 32);
        if (resolvedBars < 8)
            resolvedBars = 8;

        ScaleType resolvedScaleType;
        if (!string.IsNullOrWhiteSpace(scaleType) && Enum.TryParse(scaleType, true, out ScaleType parsedScaleType))
        {
            resolvedScaleType = parsedScaleType;
        }
        else if (!string.IsNullOrWhiteSpace(key))
        {
            resolvedScaleType = Key.Parse(key).ScaleType;
        }
        else
        {
            resolvedScaleType = ProgressionDatabase.GetDefaultScaleType(mood);
        }

        string resolvedKey = ResolveKeyName(key, resolvedScaleType, mood, genre);
        int resolvedTempo = tempo ?? ProgressionDatabase.GetDefaultTempo(genre);
        float energy = ResolveEnergy(prompt, mood, genre);
        string resolvedPalette = ResolvePaletteName(palette, prompt, mood, genre);
        string resolvedForm = ResolveFormHint(form, prompt, loop ?? true, resolvedBars);

        return new CompositionSpec(
            string.IsNullOrWhiteSpace(title) ? "Untitled Cue" : title.Trim(),
            prompt?.Trim() ?? "",
            genre,
            mood,
            resolvedBars,
            loop ?? true,
            resolvedKey,
            resolvedScaleType,
            resolvedTempo,
            resolvedPalette,
            seed,
            energy,
            resolvedForm);
    }

    public SongCompositionResult Compose(CompositionSpec spec)
    {
        var candidates = BuildCandidateSpecs(spec)
            .Select((candidateSpec, index) => (index, result: ComposeSingleCandidate(candidateSpec, index)))
            .ToList();

        int bestIndex = candidates
            .OrderByDescending(candidate => ScoreCandidate(candidate.result.Metadata.Analysis))
            .First().index;

        var selected = candidates.First(candidate => candidate.index == bestIndex).result;
        var candidateSummaries = candidates.Select(candidate => new CompositionCandidateSummary(
            candidate.index,
            candidate.result.Metadata.Spec?.Seed,
            candidate.result.Metadata.Analysis?.OverallScore ?? 0,
            candidate.result.Metadata.ArrangementPlan?.Form ?? "",
            candidate.result.Metadata.Analysis?.Findings ?? [],
            candidate.result.Metadata.WarningList)).ToArray();

        var metadata = selected.Metadata with
        {
            CandidateSummaries = candidateSummaries,
            SelectedCandidateIndex = bestIndex
        };

        if (metadata.Analysis != null && (metadata.Analysis.OverallScore < 0.70 || metadata.Analysis.Findings.Count > 2))
        {
            var revised = _revisionEngine.Revise(selected.Song, metadata, "reduce repetition and tighten loop seam");
            metadata = revised.Metadata with
            {
                CandidateSummaries = candidateSummaries,
                SelectedCandidateIndex = bestIndex,
                Warnings = revised.Metadata.WarningList.Concat(["Applied one automatic repair pass."]).Distinct().ToArray()
            };
            selected = new SongCompositionResult(revised.Song, metadata);
        }
        else
        {
            selected = new SongCompositionResult(selected.Song, metadata);
        }

        return selected;
    }

    public ArrangementPlan BuildArrangementPlan(CompositionSpec spec, Random? random = null)
    {
        random ??= spec.Seed.HasValue ? new Random(spec.Seed.Value) : new Random();

        const int sectionBars = 4;
        int sectionCount = Math.Max(1, spec.Bars / sectionBars);
        var sections = new List<ArrangementSection>(sectionCount);

        var suggestions = new ChordProgressionGenerator(spec.Seed)
            .GenerateMultiple(new ProgressionOptions(spec.ToKey(), spec.Mood, spec.Genre, sectionBars), count: Math.Max(2, Math.Min(4, sectionCount)));

        string[] aChords = suggestions.FirstOrDefault()?.Chords.Select(c => c.Chord.ToString()).ToArray() ?? ["Am", "F", "C", "G"];
        string[] bChords = suggestions.Skip(1).FirstOrDefault()?.Chords.Select(c => c.Chord.ToString()).ToArray() ?? aChords.Reverse().ToArray();
        string[] cChords = suggestions.Skip(2).FirstOrDefault()?.Chords.Select(c => c.Chord.ToString()).ToArray() ?? Rotate(aChords, 1);

        var templates = BuildSectionTemplates(sectionCount, spec.FormHint, spec.Loop);
        int startBar = 0;
        foreach (var template in templates)
        {
            string[] chords = template.MaterialKey switch
            {
                "B" => bChords,
                "C" => cChords,
                _ => aChords
            };

            sections.Add(new ArrangementSection(
                template.Label,
                startBar,
                sectionBars,
                template.Function,
                template.Intensity,
                chords,
                template.MaterialKey,
                template.VariationOf));

            startBar += sectionBars;
        }

        string form = string.Join(" / ", sections.Select(s => s.Label));
        return new ArrangementPlan(spec.Bars, spec.Loop, form, sections);
    }

    public static ChordProgression ProgressionFromChords(Key key, IReadOnlyList<string> chords) =>
        new()
        {
            Key = key,
            Chords = chords.Select(chord => new ChordEvent(Chord.Parse(chord))).ToList(),
            TemplateName = "Resolved Section"
        };

    public static NoteSequence VarySequence(NoteSequence source, Key key, float intensity, int? seed = null, bool aggressive = false)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var varied = new List<NoteEvent>();
        var scale = key.Scale;

        foreach (var evt in source.Events)
        {
            if (evt.IsRest)
            {
                varied.Add(evt);
                continue;
            }

            var note = evt.Note;
            float velocity = Math.Clamp(evt.Velocity + ((float)random.NextDouble() - 0.5f) * 0.15f, 0.35f, 0.98f);
            float duration = evt.DurationBeats;

            if (random.NextDouble() < (aggressive ? 0.45 : 0.28))
            {
                int direction = random.Next(2) == 0 ? -1 : 1;
                int? degree = scale.GetDegreeOf(note);
                if (degree.HasValue)
                {
                    int nextDegree = Math.Clamp(degree.Value + direction, 1, scale.Intervals.Length);
                    var variedNote = scale.GetDegree(nextDegree, note.Octave);
                    if (Math.Abs(variedNote.MidiNumber - note.MidiNumber) <= 5)
                        note = variedNote;
                }
            }

            if (random.NextDouble() < (aggressive ? 0.35 : 0.18))
                duration = Math.Clamp(evt.DurationBeats + (random.Next(2) == 0 ? -0.25f : 0.25f), 0.25f, 2f);

            varied.Add(evt with { Note = note, DurationBeats = duration, Velocity = velocity });
        }

        if (varied.Count > 0)
        {
            var last = varied[^1];
            if (!last.IsRest)
            {
                var tonic = key.Scale.GetDegree(1, last.Note.Octave);
                varied[^1] = last with
                {
                    Note = aggressive && random.NextDouble() < 0.5
                        ? tonic.Transpose(-12).MidiNumber < 48 ? tonic : tonic.Transpose(-12)
                        : tonic,
                    Velocity = Math.Clamp(last.Velocity + intensity * 0.12f, 0.4f, 0.98f)
                };
            }
        }

        return new NoteSequence
        {
            Events = varied.OrderBy(e => e.StartBeat).ToList(),
            TotalBars = source.TotalBars,
            BeatsPerBar = source.BeatsPerBar
        };
    }

    public static DrumStyle ResolveDrumStyle(Genre genre, Mood mood) => (genre, mood) switch
    {
        (Genre.Horror, _) => DrumStyle.HalfTime,
        (Genre.RpgTown, _) => DrumStyle.Shuffle,
        (Genre.Platformer, Mood.Playful) => DrumStyle.FourOnFloor,
        (Genre.Space, _) => DrumStyle.Breakbeat,
        (_, Mood.Calm) => DrumStyle.HalfTime,
        (_, Mood.Dark) => DrumStyle.Breakbeat,
        _ => DrumStyle.StraightRock
    };

    private static IReadOnlyList<(string Label, string Function, float Intensity, string MaterialKey, string? VariationOf)> BuildSectionTemplates(
        int sectionCount,
        string? formHint,
        bool loop)
    {
        string normalizedForm = NormalizeFormHint(formHint, loop);
        return normalizedForm switch
        {
            "linear-arc" => BuildLinearArcTemplates(sectionCount),
            "mini-song" => BuildMiniSongTemplates(sectionCount, loop),
            _ => BuildLoopVariationTemplates(sectionCount)
        };
    }

    private static IReadOnlyList<(string Label, string Function, float Intensity, string MaterialKey, string? VariationOf)> BuildLoopVariationTemplates(int sectionCount) => sectionCount switch
    {
        <= 2 =>
        [
            ("A", "statement", 0.58f, "A", null),
            ("B", "turnaround", 0.72f, "B", null)
        ],
        3 =>
        [
            ("A", "statement", 0.56f, "A", null),
            ("A'", "variation", 0.66f, "A", "A"),
            ("B", "contrast", 0.76f, "B", null)
        ],
        4 =>
        [
            ("A", "statement", 0.56f, "A", null),
            ("A'", "variation", 0.64f, "A", "A"),
            ("B", "contrast", 0.78f, "B", null),
            ("A''", "return", 0.74f, "A", "A")
        ],
        _ =>
        [
            ("A", "statement", 0.54f, "A", null),
            ("A'", "variation", 0.62f, "A", "A"),
            ("B", "contrast", 0.76f, "B", null),
            ("A''", "return", 0.70f, "A", "A"),
            ("C", "bridge", 0.64f, "C", null),
            ("A'''", "variation", 0.72f, "A", "A"),
            ("B'", "lift", 0.82f, "B", "B"),
            ("A''''", "final return", 0.78f, "A", "A")
        ]
    };

    private static IReadOnlyList<(string Label, string Function, float Intensity, string MaterialKey, string? VariationOf)> BuildMiniSongTemplates(int sectionCount, bool loop) => sectionCount switch
    {
        <= 2 =>
        [
            ("Intro", "intro", 0.46f, "A", null),
            (loop ? "Hook" : "Climax", loop ? "hook" : "climax", 0.74f, "B", null)
        ],
        3 =>
        [
            ("Intro", "intro", 0.44f, "A", null),
            ("A", "statement", 0.62f, "A", null),
            ("B", "contrast", 0.78f, "B", null)
        ],
        4 =>
        [
            ("Intro", "intro", 0.42f, "A", null),
            ("A", "statement", 0.60f, "A", null),
            ("B", "contrast", 0.76f, "B", null),
            (loop ? "Hook" : "Outro", loop ? "hook" : "outro", loop ? 0.82f : 0.56f, loop ? "C" : "A", loop ? null : "A")
        ],
        _ =>
        [
            ("Intro", "intro", 0.42f, "A", null),
            ("A", "statement", 0.58f, "A", null),
            ("B", "contrast", 0.74f, "B", null),
            ("Bridge", "bridge", 0.66f, "C", null),
            ("Hook", "hook", 0.82f, "C", null),
            ("A'", "return", 0.68f, "A", "A"),
            ("Lift", "lift", 0.78f, "B", "B"),
            (loop ? "Hook'" : "Outro", loop ? "final hook" : "outro", loop ? 0.80f : 0.52f, loop ? "C" : "A", loop ? "Hook" : "A")
        ]
    };

    private static IReadOnlyList<(string Label, string Function, float Intensity, string MaterialKey, string? VariationOf)> BuildLinearArcTemplates(int sectionCount) => sectionCount switch
    {
        <= 2 =>
        [
            ("Intro", "intro", 0.44f, "A", null),
            ("Climax", "climax", 0.84f, "B", null)
        ],
        3 =>
        [
            ("Intro", "intro", 0.42f, "A", null),
            ("A", "statement", 0.62f, "A", null),
            ("Outro", "outro", 0.50f, "C", null)
        ],
        4 =>
        [
            ("Intro", "intro", 0.40f, "A", null),
            ("A", "statement", 0.58f, "A", null),
            ("B", "lift", 0.74f, "B", null),
            ("Climax", "climax", 0.86f, "C", null)
        ],
        _ =>
        [
            ("Intro", "intro", 0.38f, "A", null),
            ("A", "statement", 0.54f, "A", null),
            ("A'", "development", 0.62f, "A", "A"),
            ("B", "contrast", 0.72f, "B", null),
            ("Bridge", "bridge", 0.64f, "C", null),
            ("Lift", "lift", 0.78f, "B", "B"),
            ("Climax", "climax", 0.88f, "C", null),
            ("Outro", "outro", 0.46f, "A", "A")
        ]
    };

    internal static Pattern ComposeSectionPattern(
        CompositionSpec spec,
        ArrangementSection section,
        PaletteProfile palette,
        int sectionIndex)
    {
        var progression = ProgressionFromChords(spec.ToKey(), section.Chords);
        return BuildSectionPattern(spec, section, progression, palette, sectionIndex);
    }

    private SongCompositionResult ComposeSingleCandidate(CompositionSpec spec, int candidateIndex)
    {
        var random = spec.Seed.HasValue ? new Random(spec.Seed.Value) : new Random();
        var palette = PaletteProfileLibrary.Resolve(spec.Palette, spec.Mood, spec.Genre);
        var arrangement = BuildArrangementPlan(spec, random);
        var warnings = new List<string>();

        var song = new Song
        {
            Title = spec.Title,
            Tempo = spec.Tempo,
            RowsPerBeat = spec.RowsPerBeat,
            Author = "ChipCraft Composer",
            MasterVolume = 0.82f
        };

        song.InitializeChannels(palette.ChannelCount);
        ApplyPalette(song, palette);

        for (int index = 0; index < arrangement.Sections.Count; index++)
        {
            var section = arrangement.Sections[index];
            var pattern = ComposeSectionPattern(spec, section, palette, index);
            song.Patterns.Add(pattern);
            song.AddToOrder(song.Patterns.Count - 1);
        }

        if (spec.Loop)
            song.OrderList.LoopStartIndex = 0;

        ApplyPerformanceShape(song, arrangement, spec);

        var metadata = new SongProjectMetadata(spec, arrangement, palette.Assignments, Warnings: warnings);
        var analysis = _analyzer.Analyze(song, metadata);
        metadata = metadata with { Analysis = analysis, Warnings = warnings.Concat(analysis.Warnings).Distinct().ToArray() };
        return new SongCompositionResult(song, metadata);
    }

    private static IReadOnlyList<CompositionSpec> BuildCandidateSpecs(CompositionSpec spec)
    {
        int count = spec.Bars >= 24 ? 4 : 3;
        if (spec.Seed.HasValue)
        {
            return Enumerable.Range(0, count)
                .Select(index => spec with { Seed = spec.Seed.Value + index * 977 })
                .ToArray();
        }

        var random = new Random();
        return Enumerable.Range(0, count)
            .Select(_ => spec with { Seed = random.Next(1, int.MaxValue) })
            .ToArray();
    }

    private static double ScoreCandidate(SongAnalysis? analysis)
    {
        if (analysis == null)
            return double.NegativeInfinity;

        return analysis.OverallScore
            - analysis.Findings.Count * 0.04
            - analysis.Warnings.Count * 0.01;
    }

    private static Pattern BuildSectionPattern(
        CompositionSpec spec,
        ArrangementSection section,
        ChordProgression progression,
        PaletteProfile palette,
        int sectionIndex)
    {
        int rows = section.Bars * spec.RowsPerBeat * 4;
        var pattern = new Pattern(rows, palette.ChannelCount) { Name = section.Label };
        int? baseSeed = spec.Seed;

        int leadChannel = palette.Assignments.First(a => a.Role == ChannelRole.Lead).Channel;
        int bassChannel = palette.Assignments.First(a => a.Role == ChannelRole.Bass).Channel;
        int drumChannel = palette.Assignments.First(a => a.Role == ChannelRole.Drums).Channel;
        int harmonyChannel = palette.Assignments.First(a => a.Role == ChannelRole.Harmony).Channel;
        int padLowChannel = palette.Assignments.First(a => a.Role == ChannelRole.PadLow).Channel;
        int padHighChannel = palette.Assignments.First(a => a.Role == ChannelRole.PadHigh).Channel;

        MelodyContour contour = ResolveContour(section.Label, spec.Mood);
        var lead = new MelodyGenerator(baseSeed.HasValue ? baseSeed.Value + sectionIndex * 17 : null)
            .Generate(new MelodyOptions(
                spec.ToKey(),
                contour,
                section.Bars,
                Progression: progression,
                Energy: Math.Clamp(spec.Energy + (section.Intensity - 0.6f) * 0.4f, 0.25f, 0.95f),
                RestProbability: spec.Mood is Mood.Calm or Mood.Melancholy ? 0.14f : 0.08f,
                LowNote: ResolveLeadRangeLow(spec.Mood, spec.Genre),
                HighNote: ResolveLeadRangeHigh(spec.Mood, spec.Genre)));

        if (!string.IsNullOrEmpty(section.VariationOf))
        {
            lead = VarySequence(
                lead,
                spec.ToKey(),
                section.Intensity,
                baseSeed.HasValue ? baseSeed.Value + 1000 + sectionIndex : null,
                aggressive: section.Label.Contains("''", StringComparison.Ordinal));
        }

        var bassStyle = ResolveBassStyle(spec.Genre, spec.Mood, section.Intensity);
        var bass = new BassLineGenerator(baseSeed.HasValue ? baseSeed.Value + sectionIndex * 23 : null)
            .Generate(new BassLineOptions(progression, bassStyle, Octave: 2, Energy: spec.Energy));

        int drumEnergy = (int)Math.Clamp(Math.Round(spec.Energy * 7 + section.Intensity * 2), 2, 10);
        var drums = new DrumPatternGenerator(baseSeed.HasValue ? baseSeed.Value + sectionIndex * 29 : null)
            .Generate(new DrumPatternOptions(ResolveDrumStyle(spec.Genre, spec.Mood), drumEnergy, section.Bars, Fills: true));

        HarmonyStyle harmonyStyle = ResolveHarmonyStyle(spec.Genre, spec.Mood);
        var harmony = new HarmonyGenerator(baseSeed.HasValue ? baseSeed.Value + sectionIndex * 31 : null)
            .Generate(new HarmonyOptions(spec.ToKey(), lead, progression, harmonyStyle));

        var padVoices = new PadGenerator()
            .GenerateVoicings(progression, voiceCount: 2, octave: spec.Mood is Mood.Dark or Mood.Melancholy ? 4 : 5);

        pattern.ApplyNoteSequence(lead, leadChannel, rowsPerBeat: spec.RowsPerBeat);
        pattern.ApplyNoteSequence(bass, bassChannel, rowsPerBeat: spec.RowsPerBeat);
        pattern.ApplyDrumPattern(drums, drumChannel, spec.RowsPerBeat);
        pattern.ApplyNoteSequence(harmony, harmonyChannel, rowsPerBeat: spec.RowsPerBeat);
        pattern.ApplyNoteSequence(padVoices[0], padLowChannel, rowsPerBeat: spec.RowsPerBeat);
        pattern.ApplyNoteSequence(padVoices[1], padHighChannel, rowsPerBeat: spec.RowsPerBeat);

        return pattern;
    }

    private static BassStyle ResolveBassStyle(Genre genre, Mood mood, float intensity) => (genre, mood, intensity) switch
    {
        (_, _, >= 0.78f) => BassStyle.Arpeggiated,
        (_, Mood.Calm, _) => BassStyle.Pedal,
        (_, Mood.Melancholy, _) => BassStyle.Walking,
        (Genre.RpgTown, _, _) => BassStyle.Walking,
        (Genre.Horror, _, _) => BassStyle.Pedal,
        _ => BassStyle.RootFifth
    };

    private static HarmonyStyle ResolveHarmonyStyle(Genre genre, Mood mood) => (genre, mood) switch
    {
        (_, Mood.Mysterious) => HarmonyStyle.Countermelody,
        (_, Mood.Playful) => HarmonyStyle.SixthsBelow,
        (_, Mood.Calm) => HarmonyStyle.ThirdsBelow,
        _ => HarmonyStyle.ThirdsBelow
    };

    private static MelodyContour ResolveContour(string label, Mood mood) => (label, mood) switch
    {
        ("A", Mood.Calm) => MelodyContour.Flat,
        ("B", _) => MelodyContour.Arch,
        _ when label.StartsWith("A", StringComparison.Ordinal) => MelodyContour.Arch,
        _ when mood is Mood.Dark or Mood.Mysterious => MelodyContour.Descending,
        _ => MelodyContour.Ascending
    };

    private static Note ResolveLeadRangeLow(Mood mood, Genre genre) => (mood, genre) switch
    {
        (Mood.Calm, _) => Note.Parse("G4"),
        (Mood.Playful, _) => Note.Parse("A4"),
        _ => Note.Parse("C4")
    };

    private static Note ResolveLeadRangeHigh(Mood mood, Genre genre) => (mood, genre) switch
    {
        (Mood.Dark, _) => Note.Parse("C5"),
        (Mood.Melancholy, _) => Note.Parse("D5"),
        _ => Note.Parse("C6")
    };

    private static void ApplyPalette(Song song, PaletteProfile palette)
    {
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

            song.ChannelVolumes[assignment.Channel] = assignment.Volume;
            song.ChannelPans[assignment.Channel] = assignment.Pan;
        }
    }

    private static string ResolveKeyName(string? key, ScaleType scaleType, Mood mood, Genre genre)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var parsed = Key.Parse(key);
            return new Key(parsed.Root, scaleType).ToString();
        }

        string root = (mood, genre) switch
        {
            (Mood.Calm, _) => "C",
            (Mood.Playful, _) => "G",
            (Mood.Mysterious, _) => "Dm",
            (Mood.Dark, _) => "Em",
            (_, Genre.Space) => "Fm",
            _ => ScaleDatabase.IsMinor(scaleType) ? "Am" : "C"
        };

        var resolved = Key.Parse(root);
        return new Key(resolved.Root, scaleType).ToString();
    }

    private static float ResolveEnergy(string prompt, Mood mood, Genre genre)
    {
        string lower = prompt?.ToLowerInvariant() ?? "";
        float moodBase = mood switch
        {
            Mood.Calm => 0.40f,
            Mood.Playful => 0.60f,
            Mood.Melancholy => 0.48f,
            Mood.Mysterious => 0.55f,
            Mood.Dark => 0.62f,
            Mood.Tense => 0.72f,
            Mood.Urgent => 0.82f,
            Mood.Epic => 0.78f,
            Mood.Heroic => 0.70f,
            Mood.Triumphant => 0.74f,
            _ => 0.60f
        };

        if (lower.Contains("gentle") || lower.Contains("soft") || lower.Contains("ambient"))
            moodBase -= 0.12f;
        if (lower.Contains("driving") || lower.Contains("intense") || lower.Contains("aggressive"))
            moodBase += 0.12f;
        if (genre is Genre.Action or Genre.RpgBattle or Genre.Sports)
            moodBase += 0.05f;

        return Math.Clamp(moodBase, 0.25f, 0.95f);
    }

    private static string[] Rotate(string[] source, int offset)
    {
        if (source.Length == 0)
            return source;

        return Enumerable.Range(0, source.Length)
            .Select(index => source[(index + offset) % source.Length])
            .ToArray();
    }

    private static void ApplyPerformanceShape(Song song, ArrangementPlan arrangement, CompositionSpec spec)
    {
        if (spec.Loop && !spec.FormHint.Contains("linear", StringComparison.OrdinalIgnoreCase))
            return;

        for (int index = 0; index < arrangement.Sections.Count && index < song.OrderList.Entries.Count; index++)
        {
            var section = arrangement.Sections[index];
            int shapedTempo = Math.Clamp(spec.Tempo + ResolveTempoOffset(section), 60, 220);
            if (shapedTempo == spec.Tempo)
                continue;

            song.OrderList.Entries[index] = song.OrderList.Entries[index] with { TempoOverride = shapedTempo };
        }
    }

    private static int ResolveTempoOffset(ArrangementSection section) => section.Function switch
    {
        "intro" => -6,
        "outro" => -10,
        "bridge" => -3,
        "contrast" => 2,
        "lift" => 5,
        "hook" => 4,
        "final hook" => 3,
        "climax" => 7,
        _ => section.Intensity >= 0.78f ? 3 : 0
    };

    private static string ResolvePaletteName(string? palette, string prompt, Mood mood, Genre genre)
    {
        if (!string.IsNullOrWhiteSpace(palette))
            return palette.Trim();

        string lower = prompt?.ToLowerInvariant() ?? "";
        if (lower.Contains("orchestral") || lower.Contains("cinematic"))
            return "cinematic";
        if (lower.Contains("ambient") || lower.Contains("atmospheric") || lower.Contains("menu"))
            return "ambient";
        if (lower.Contains("retro") || lower.Contains("8-bit") || lower.Contains("chiptune"))
            return "retro-console";
        if (lower.Contains("boss"))
            return "boss-battle";

        return PaletteProfileLibrary.Resolve(null, mood, genre).Name;
    }

    private static string ResolveFormHint(string? form, string prompt, bool loop, int bars)
    {
        if (!string.IsNullOrWhiteSpace(form))
            return NormalizeFormHint(form, loop);

        string lower = prompt?.ToLowerInvariant() ?? "";
        if (!loop || lower.Contains("linear") || lower.Contains("through composed") || lower.Contains("story arc"))
            return "linear-arc";
        if (lower.Contains("mini-song") || lower.Contains("full cue") || lower.Contains("journey") || bars >= 24)
            return "mini-song";

        return "loop-variation";
    }

    private static string NormalizeFormHint(string? formHint, bool loop)
    {
        if (string.IsNullOrWhiteSpace(formHint))
            return loop ? "loop-variation" : "linear-arc";

        string normalized = formHint.Trim().ToLowerInvariant();
        return normalized switch
        {
            "linear" or "linear-arc" or "through-composed" or "through composed" => "linear-arc",
            "mini-song" or "mini song" or "song" => "mini-song",
            _ => "loop-variation"
        };
    }
}
