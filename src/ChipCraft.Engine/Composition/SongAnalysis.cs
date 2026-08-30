namespace ChipCraft.Engine.Composition;

public record AnalysisMetric(string Name, double Score, string Summary);

public record SongAnalysis(
    AnalysisMetric LoopQuality,
    AnalysisMetric PhraseVariation,
    AnalysisMetric RegisterSeparation,
    AnalysisMetric RhythmicDensity,
    AnalysisMetric HarmonicFit,
    AnalysisMetric MelodyMemorability,
    AnalysisMetric SectionContrast,
    AnalysisMetric CadenceStrength,
    AnalysisMetric ChannelCrowding,
    AnalysisMetric RoleCoverage,
    AnalysisMetric ExportReadiness,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Warnings
)
{
    public double OverallScore =>
        (LoopQuality.Score + PhraseVariation.Score + RegisterSeparation.Score +
         RhythmicDensity.Score + HarmonicFit.Score + MelodyMemorability.Score +
         SectionContrast.Score + CadenceStrength.Score + ChannelCrowding.Score +
         RoleCoverage.Score + ExportReadiness.Score) / 11.0;

    public bool ReadyForExport => OverallScore >= 0.68 && Findings.Count <= 2;
}
