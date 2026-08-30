using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Persistence;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class PersistenceTools
{
    [McpServerTool(Name = "save_song"), Description("Persist a song to JSON and write a companion manifest with composition metadata.")]
    public static string SaveSong(
        SessionState session,
        [Description("Song ID. Omit to save the most recent song.")] string? songId = null,
        [Description("Optional output directory. Defaults to ./sample-outputs/<slug>-<timestamp>.")] string? outputDir = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = "No song found." });

        var metadata = session.GetSongMetadata(song.Id) ?? new SongProjectMetadata(null, null, []);
        string resolvedDir = SongArtifactUtilities.ResolveOutputDirectory(song.Title, outputDir);
        var artifacts = SongArtifactUtilities.ExportArtifacts(
            song,
            metadata,
            resolvedDir,
            renderPreview: false,
            soundFontPath: null,
            exportStems: metadata.Artifacts?.StemList.Count > 0);

        session.SetSongMetadata(song.Id, metadata with
        {
            Artifacts = artifacts
        });

        return JsonSerializer.Serialize(new
        {
            songId = song.Id,
            artifacts.SongJsonPath,
            artifacts.ManifestPath,
            artifacts.MidiPath,
            stemPaths = artifacts.StemList
        });
    }

    [McpServerTool(Name = "load_song"), Description("Load a previously saved song JSON or manifest into the current MCP session.")]
    public static string LoadSong(
        SessionState session,
        [Description("Path to a .song.json file or a manifest.json file.")] string inputPath)
    {
        string fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
            return JsonSerializer.Serialize(new { error = $"File not found: {fullPath}" });

        SongManifest? manifest = null;
        string songJsonPath = fullPath;
        if (Path.GetFileName(fullPath).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            manifest = JsonSerializer.Deserialize<SongManifest>(File.ReadAllText(fullPath), McpToolJson.SerializerOptions);
            if (manifest?.Artifacts?.SongJsonPath == null)
                return JsonSerializer.Serialize(new { error = "Manifest does not contain a song JSON path." });

            songJsonPath = Path.GetFullPath(manifest.Artifacts.SongJsonPath);
        }

        var song = SongSerializer.Deserialize(File.ReadAllText(songJsonPath));
        string songId = session.AddSong(song);

        if (manifest != null)
        {
            session.SetSongMetadata(songId, new SongProjectMetadata(
                manifest.Spec,
                manifest.ArrangementPlan,
                manifest.ChannelAssignments,
                manifest.Analysis,
                manifest.Artifacts,
                manifest.CandidateSummaries,
                manifest.SelectedCandidateIndex,
                manifest.Warnings));
        }

        return JsonSerializer.Serialize(new
        {
            songId,
            title = song.Title,
            loadedFrom = songJsonPath,
            manifestLoaded = manifest != null
        });
    }
}
