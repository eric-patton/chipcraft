using ChipCraft.Engine.Midi;
using NAudio.Wave;

namespace ChipCraft.Engine.Tests.Midi;

public class RenderAnalysisTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "chipcraft_render_analysis_tests", Guid.NewGuid().ToString("N"));

    public RenderAnalysisTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }

    [Fact]
    public void Analyze_ClippedAndImbalancedAudioProducesFindings()
    {
        string path = Path.Combine(_outputDir, "clipped.wav");
        WriteWave(path, 2, 44100, 44100, (frame, channel) =>
        {
            float baseSample = MathF.Sin(frame / 20f);
            return channel == 0 ? baseSample : baseSample * 0.06f;
        });

        var analysis = new RenderedAudioAnalyzer().Analyze(path);

        Assert.True(analysis.PeakHeadroom.Score < 0.55);
        Assert.True(analysis.StereoBalance.Score < 0.55);
        Assert.Contains(analysis.Findings, finding => finding.Contains("0 dBFS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.Findings, finding => finding.Contains("stereo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_SilentAudioFlagsLoudnessProblems()
    {
        string path = Path.Combine(_outputDir, "silent.wav");
        WriteWave(path, 1, 44100, 22050, (_, _) => 0f);

        var analysis = new RenderedAudioAnalyzer().Analyze(path);

        Assert.True(analysis.LoudnessTarget.Score < 0.1);
        Assert.Contains(analysis.Findings, finding => finding.Contains("outside the preferred range", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_HardCutTailFlagsTailCleanup()
    {
        string path = Path.Combine(_outputDir, "hard-cut.wav");
        WriteWave(path, 1, 44100, 22050, (frame, _) => MathF.Sin(frame / 30f) * 0.4f);

        var analysis = new RenderedAudioAnalyzer().Analyze(path);

        Assert.True(analysis.TailCleanup.Score < 0.55);
        Assert.Contains(analysis.Findings, finding => finding.Contains("abruptly", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteWave(string path, int channels, int sampleRate, int frames, Func<int, int, float> sampleProvider)
    {
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels));
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
                writer.WriteSample(sampleProvider(frame, channel));
        }
    }
}
