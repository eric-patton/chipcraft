using System.Text.Json.Serialization;

namespace ChipCraft.Engine.Composition;

public record CompositionArtifacts(
    string? OutputDirectory = null,
    string? MidiPath = null,
    string? SongJsonPath = null,
    string? ManifestPath = null,
    string? PreviewWavPath = null,
    IReadOnlyList<StemArtifact>? StemArtifacts = null
)
{
    [JsonIgnore]
    public IReadOnlyList<StemArtifact> StemList => StemArtifacts ?? [];
}

public record StemArtifact(
    string Name,
    string MidiPath,
    string? PreviewWavPath,
    IReadOnlyList<int> Channels,
    IReadOnlyList<string> Roles
);

public record CompositionCandidateSummary(
    int Index,
    int? Seed,
    double OverallScore,
    string Form,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Warnings
);

public record SongProjectMetadata(
    CompositionSpec? Spec,
    ArrangementPlan? ArrangementPlan,
    IReadOnlyList<ChannelRoleAssignment> ChannelAssignments,
    SongAnalysis? Analysis = null,
    CompositionArtifacts? Artifacts = null,
    IReadOnlyList<CompositionCandidateSummary>? CandidateSummaries = null,
    int? SelectedCandidateIndex = null,
    IReadOnlyList<string>? Warnings = null
)
{
    [JsonIgnore]
    public IReadOnlyList<string> WarningList => Warnings ?? [];
    [JsonIgnore]
    public IReadOnlyList<CompositionCandidateSummary> CandidateList => CandidateSummaries ?? [];
}

public record SongCompositionResult(
    Sequencer.Song Song,
    SongProjectMetadata Metadata
);

public record SongManifest(
    string SongId,
    string Title,
    CompositionSpec? Spec,
    ArrangementPlan? ArrangementPlan,
    IReadOnlyList<ChannelRoleAssignment> ChannelAssignments,
    SongAnalysis? Analysis,
    CompositionArtifacts? Artifacts,
    IReadOnlyList<CompositionCandidateSummary>? CandidateSummaries,
    int? SelectedCandidateIndex,
    IReadOnlyList<string> Warnings,
    DateTime CreatedAtUtc
);
