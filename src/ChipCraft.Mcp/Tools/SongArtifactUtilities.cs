using System.Text;
using System.Text.Json;
using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Persistence;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Mcp.Tools;

internal static class SongArtifactUtilities
{
    internal static CompositionArtifacts ExportArtifacts(
        Song song,
        SongProjectMetadata metadata,
        string outputDir,
        bool renderPreview,
        string? soundFontPath,
        bool exportStems = false)
    {
        Directory.CreateDirectory(outputDir);
        string slug = Slugify(song.Title);
        string midiPath = Path.Combine(outputDir, $"{slug}.mid");
        string songJsonPath = Path.Combine(outputDir, $"{slug}.song.json");
        string manifestPath = Path.Combine(outputDir, "manifest.json");
        string? previewPath = renderPreview && !string.IsNullOrWhiteSpace(soundFontPath)
            ? Path.Combine(outputDir, $"{slug}.preview.wav")
            : null;

        new MidiExporter().Export(song, midiPath, metadata);
        File.WriteAllText(songJsonPath, SongSerializer.Serialize(song), Encoding.UTF8);

        if (previewPath != null && File.Exists(soundFontPath))
            new AudioRenderer(soundFontPath!).RenderToWav(song, previewPath);

        StemArtifact[] stemArtifacts = exportStems
            ? ExportStems(song, metadata, outputDir, slug, renderPreview, soundFontPath)
            : [];

        var manifest = new SongManifest(
            song.Id,
            song.Title,
            metadata.Spec,
            metadata.ArrangementPlan,
            metadata.ChannelAssignments,
            metadata.Analysis,
            new CompositionArtifacts(outputDir, midiPath, songJsonPath, manifestPath, previewPath, stemArtifacts),
            metadata.CandidateList,
            metadata.SelectedCandidateIndex,
            metadata.WarningList,
            DateTime.UtcNow);

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, McpToolJson.SerializerOptions), Encoding.UTF8);
        return new CompositionArtifacts(outputDir, midiPath, songJsonPath, manifestPath, previewPath, stemArtifacts);
    }

    internal static string ResolveOutputDirectory(string title, string? outputDir)
    {
        if (!string.IsNullOrWhiteSpace(outputDir))
            return Path.GetFullPath(outputDir);

        string slug = Slugify(title);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.GetFullPath(Path.Combine(".", "sample-outputs", $"{slug}-{timestamp}"));
    }

    private static string Slugify(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        string slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }

    private static StemArtifact[] ExportStems(
        Song song,
        SongProjectMetadata metadata,
        string outputDir,
        string slug,
        bool renderPreview,
        string? soundFontPath)
    {
        if (metadata.ChannelAssignments.Count == 0)
            return [];

        string stemsDir = Path.Combine(outputDir, "stems");
        Directory.CreateDirectory(stemsDir);

        var exporter = new MidiExporter();
        var stems = new List<StemArtifact>();
        foreach (var definition in StemLayoutLibrary.Resolve(metadata.ChannelAssignments))
        {
            var channels = StemLayoutLibrary.ResolveChannels(definition, metadata.ChannelAssignments);
            if (channels.Count == 0)
                continue;

            var stemSong = CreateStemSong(song, channels);
            string midiPath = Path.Combine(stemsDir, $"{slug}.{definition.Name}.mid");
            string? previewPath = renderPreview && !string.IsNullOrWhiteSpace(soundFontPath)
                ? Path.Combine(stemsDir, $"{slug}.{definition.Name}.preview.wav")
                : null;

            exporter.Export(stemSong, midiPath, metadata);
            if (previewPath != null && File.Exists(soundFontPath))
                new AudioRenderer(soundFontPath!).RenderToWav(stemSong, previewPath);

            stems.Add(new StemArtifact(
                definition.Name,
                midiPath,
                previewPath,
                channels,
                definition.Roles.Select(role => role.ToString()).ToArray()));
        }

        return stems.ToArray();
    }

    private static Song CreateStemSong(Song song, IReadOnlyList<int> includedChannels)
    {
        var stemSong = SongSerializer.Deserialize(SongSerializer.Serialize(song));
        var included = includedChannels.ToHashSet();

        foreach (var pattern in stemSong.Patterns)
        {
            for (int channel = 0; channel < pattern.ChannelCount; channel++)
            {
                if (included.Contains(channel))
                    continue;

                for (int row = 0; row < pattern.RowCount; row++)
                    pattern.ClearCell(row, channel);
            }

            pattern.Parts.RemoveAll(part => !included.Contains(part.Channel));
        }

        for (int channel = 0; channel < stemSong.ChannelCount; channel++)
        {
            if (included.Contains(channel))
                continue;

            if (channel < stemSong.ChannelVolumes.Length)
                stemSong.ChannelVolumes[channel] = 0f;
            if (channel < stemSong.ChannelMutes.Length)
                stemSong.ChannelMutes[channel] = true;
            if (channel < stemSong.ChannelSolos.Length)
                stemSong.ChannelSolos[channel] = false;
        }

        return stemSong;
    }
}
