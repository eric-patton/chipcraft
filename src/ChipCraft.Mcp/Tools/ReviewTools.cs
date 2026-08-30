using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Midi;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class ReviewTools
{
    [McpServerTool(Name = "analyze_song"), Description("Score an existing song for loop quality, phrase variation, register separation, rhythmic density, harmonic fit, role coverage, and export readiness.")]
    public static string AnalyzeSong(
        SessionState session,
        [Description("Song ID. Omit to analyze the most recent song.")] string? songId = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = "No song found." });

        string resolvedId = song.Id;
        var metadata = session.GetSongMetadata(resolvedId) ?? new SongProjectMetadata(null, null, []);
        var analysis = new SongAnalyzer().Analyze(song, metadata);
        session.SetSongMetadata(resolvedId, metadata with { Analysis = analysis });
        return JsonSerializer.Serialize(analysis, McpToolJson.SerializerOptions);
    }

    [McpServerTool(Name = "explain_song"), Description("Explain an existing song in plain language for an AI or human reviewer, including strengths and weak spots.")]
    public static string ExplainSong(
        SessionState session,
        [Description("Song ID. Omit to explain the most recent song.")] string? songId = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = "No song found." });

        var metadata = session.GetSongMetadata(song.Id);
        string explanation = new SongAnalyzer().Explain(song, metadata);
        return JsonSerializer.Serialize(new { songId = song.Id, explanation });
    }

    [McpServerTool(Name = "analyze_render"), Description("Analyze a rendered audio file for peak headroom, loudness, tail cleanup, loop seam continuity, and stereo balance.")]
    public static string AnalyzeRender(
        [Description("Audio file path to analyze.")] string audioPath)
    {
        string fullPath = Path.GetFullPath(audioPath);
        if (!File.Exists(fullPath))
            return JsonSerializer.Serialize(new { error = $"Audio file not found: {fullPath}" });

        var analysis = new RenderedAudioAnalyzer().Analyze(fullPath);
        return JsonSerializer.Serialize(analysis, McpToolJson.SerializerOptions);
    }

    [McpServerTool(Name = "review_delivery_bundle"), Description("Review a rendered delivery bundle for manifest completeness, missing assets, and final-render quality.")]
    public static string ReviewDeliveryBundle(
        [Description("Path to a delivery bundle directory or delivery-manifest.json file.")] string path)
    {
        string fullPath = Path.GetFullPath(path);
        string manifestPath = Directory.Exists(fullPath)
            ? Path.Combine(fullPath, "delivery-manifest.json")
            : fullPath;
        if (!File.Exists(manifestPath))
            return JsonSerializer.Serialize(new { error = $"Delivery manifest not found: {manifestPath}" });

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var missingFiles = new List<string>();
        var artifactFiles = new List<string>();

        if (root.TryGetProperty("artifacts", out var artifacts))
        {
            AddArtifactFile(artifacts, "midiPath", artifactFiles);
            AddArtifactFile(artifacts, "songJsonPath", artifactFiles);
            AddArtifactFile(artifacts, "fullMixPath", artifactFiles);
            AddArtifactFile(artifacts, "loopPreviewPath", artifactFiles);
            if (artifacts.TryGetProperty("stems", out var stems) && stems.ValueKind == JsonValueKind.Array)
            {
                foreach (var stem in stems.EnumerateArray())
                {
                    AddArtifactFile(stem, "midiPath", artifactFiles);
                    AddArtifactFile(stem, "wavPath", artifactFiles);
                }
            }
        }

        foreach (string artifactPath in artifactFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(artifactPath))
                missingFiles.Add(artifactPath);
        }

        RenderAnalysis? renderAnalysis = null;
        if (root.TryGetProperty("artifacts", out artifacts)
            && artifacts.TryGetProperty("fullMixPath", out var fullMixPathElement)
            && fullMixPathElement.ValueKind == JsonValueKind.String)
        {
            string? fullMixPath = fullMixPathElement.GetString();
            if (!string.IsNullOrWhiteSpace(fullMixPath) && File.Exists(fullMixPath))
                renderAnalysis = new RenderedAudioAnalyzer().Analyze(fullMixPath);
        }

        bool ready = missingFiles.Count == 0 && (renderAnalysis?.OverallScore ?? 0.8) >= 0.68;
        return JsonSerializer.Serialize(new
        {
            manifestPath,
            missingFiles,
            renderAnalysis,
            ready
        }, McpToolJson.SerializerOptions);
    }

    [McpServerTool(Name = "review_song"), Description("Run a combined musical and render review for a song, including delivery readiness.")]
    public static string ReviewSong(
        SessionState session,
        [Description("Song ID. Omit to review the most recent song.")] string? songId = null,
        [Description("Optional SoundFont path. When provided, a temporary preview render will also be analyzed.")] string? soundFontPath = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = "No song found." });

        string resolvedId = song.Id;
        var metadata = session.GetSongMetadata(resolvedId) ?? new SongProjectMetadata(null, null, []);
        var songAnalysis = new SongAnalyzer().Analyze(song, metadata);
        session.SetSongMetadata(resolvedId, metadata with { Analysis = songAnalysis });

        RenderAnalysis? renderAnalysis = null;
        string? previewPath = null;
        if (!string.IsNullOrWhiteSpace(soundFontPath))
        {
            string fullSoundFontPath = Path.GetFullPath(soundFontPath);
            if (!File.Exists(fullSoundFontPath))
                return JsonSerializer.Serialize(new { error = $"SoundFont not found: {fullSoundFontPath}" });

            previewPath = Path.Combine(Path.GetTempPath(), $"chipcraft_review_{Guid.NewGuid():N}.wav");
            try
            {
                new AudioRenderer(fullSoundFontPath).RenderToWav(song, previewPath, metadata);
                renderAnalysis = new RenderedAudioAnalyzer().Analyze(previewPath);
            }
            finally
            {
                if (previewPath != null && File.Exists(previewPath))
                    File.Delete(previewPath);
            }
        }

        bool deliveryReady = songAnalysis.ReadyForExport && (renderAnalysis?.OverallScore ?? 0.80) >= 0.68;
        return JsonSerializer.Serialize(new
        {
            songId = resolvedId,
            songAnalysis,
            renderAnalysis,
            deliveryReady
        }, McpToolJson.SerializerOptions);
    }

    private static void AddArtifactFile(JsonElement element, string propertyName, List<string> files)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return;

        string? file = property.GetString();
        if (!string.IsNullOrWhiteSpace(file))
            files.Add(Path.GetFullPath(file));
    }
}
