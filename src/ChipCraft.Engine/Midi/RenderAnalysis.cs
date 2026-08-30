using NAudio.Wave;

namespace ChipCraft.Engine.Midi;

public record RenderMetric(string Name, double Score, string Summary);

public record RenderAnalysis(
    RenderMetric PeakHeadroom,
    RenderMetric LoudnessTarget,
    RenderMetric TailCleanup,
    RenderMetric LoopSeamContinuity,
    RenderMetric StereoBalance,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Warnings)
{
    public double OverallScore =>
        (PeakHeadroom.Score + LoudnessTarget.Score + TailCleanup.Score + LoopSeamContinuity.Score + StereoBalance.Score) / 5.0;
}

public class RenderedAudioAnalyzer
{
    public RenderAnalysis Analyze(string audioPath)
    {
        using var reader = new AudioFileReader(audioPath);
        int channels = Math.Max(1, reader.WaveFormat.Channels);
        int sampleRate = reader.WaveFormat.SampleRate;
        int seamWindowFrames = Math.Max(256, Math.Min(sampleRate / 2, 8192));
        var firstWindow = new List<float>();
        var tailWindow = new Queue<float>(seamWindowFrames * channels);

        double[] sumSquares = new double[channels];
        float peak = 0f;
        long totalFrames = 0;
        long lastNonSilentFrame = -1;
        const float silenceThreshold = 0.0008f;
        var buffer = new float[4096];

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (firstWindow.Count < seamWindowFrames * channels)
            {
                int copyCount = Math.Min(read, seamWindowFrames * channels - firstWindow.Count);
                for (int i = 0; i < copyCount; i++)
                    firstWindow.Add(buffer[i]);
            }

            for (int i = 0; i < read; i++)
            {
                if (tailWindow.Count == seamWindowFrames * channels)
                    tailWindow.Dequeue();
                tailWindow.Enqueue(buffer[i]);
            }

            for (int sampleIndex = 0; sampleIndex < read; sampleIndex += channels)
            {
                bool frameIsAudible = false;
                for (int channel = 0; channel < channels && sampleIndex + channel < read; channel++)
                {
                    float sample = buffer[sampleIndex + channel];
                    peak = Math.Max(peak, Math.Abs(sample));
                    sumSquares[channel] += sample * sample;
                    frameIsAudible |= Math.Abs(sample) >= silenceThreshold;
                }

                if (frameIsAudible)
                    lastNonSilentFrame = totalFrames;
                totalFrames++;
            }
        }

        double durationSeconds = totalFrames / (double)Math.Max(1, sampleRate);
        double trailingSilenceSeconds = lastNonSilentFrame >= 0
            ? Math.Max(0, (totalFrames - lastNonSilentFrame - 1) / (double)Math.Max(1, sampleRate))
            : durationSeconds;
        double peakDb = peak > 0 ? 20.0 * Math.Log10(peak) : -120.0;
        double monoRms = Math.Sqrt(sumSquares.Sum() / Math.Max(1, totalFrames * channels));
        double loudnessDbfs = monoRms > 0 ? 20.0 * Math.Log10(monoRms) : -120.0;
        double stereoDiffDb = 0.0;
        if (channels >= 2)
        {
            double leftRms = Math.Sqrt(sumSquares[0] / Math.Max(1, totalFrames));
            double rightRms = Math.Sqrt(sumSquares[1] / Math.Max(1, totalFrames));
            double leftDb = leftRms > 0 ? 20.0 * Math.Log10(leftRms) : -120.0;
            double rightDb = rightRms > 0 ? 20.0 * Math.Log10(rightRms) : -120.0;
            stereoDiffDb = Math.Abs(leftDb - rightDb);
        }

        double seamDifference = ComputeSeamDifference(firstWindow, tailWindow.ToList(), channels);

        var peakHeadroom = AnalyzePeakHeadroom(peakDb);
        var loudnessTarget = AnalyzeLoudness(loudnessDbfs);
        var tailCleanup = AnalyzeTail(trailingSilenceSeconds, durationSeconds);
        var loopSeamContinuity = AnalyzeLoopSeam(seamDifference);
        var stereoBalance = AnalyzeStereo(stereoDiffDb, channels);

        var findings = new List<string>();
        var warnings = new List<string>();
        foreach (var metric in new[] { peakHeadroom, loudnessTarget, tailCleanup, loopSeamContinuity, stereoBalance })
        {
            if (metric.Score < 0.55)
                findings.Add(metric.Summary);
            else if (metric.Score < 0.72)
                warnings.Add(metric.Summary);
        }

        return new RenderAnalysis(
            peakHeadroom,
            loudnessTarget,
            tailCleanup,
            loopSeamContinuity,
            stereoBalance,
            findings,
            warnings);
    }

    private static RenderMetric AnalyzePeakHeadroom(double peakDb)
    {
        double score = peakDb switch
        {
            > -0.05 => 0.10,
            > -0.5 => 0.45,
            > -3.0 => 0.92,
            > -9.0 => 0.72,
            _ => 0.48
        };

        return new RenderMetric("peakHeadroom", score,
            score >= 0.72
                ? $"Peak headroom is healthy at {peakDb:0.0} dBFS."
                : peakDb > -0.5
                    ? $"Render peaks too close to 0 dBFS at {peakDb:0.0} dBFS."
                    : $"Render peak level is conservative at {peakDb:0.0} dBFS.");
    }

    private static RenderMetric AnalyzeLoudness(double loudnessDbfs)
    {
        double delta = Math.Abs(loudnessDbfs - (-16.0));
        double score = Math.Clamp(1.0 - delta / 10.0, 0.0, 1.0);
        return new RenderMetric("loudnessTarget", score,
            score >= 0.72
                ? $"Overall loudness is close to target at {loudnessDbfs:0.0} dBFS."
                : $"Overall loudness is {loudnessDbfs:0.0} dBFS, which is outside the preferred range.");
    }

    private static RenderMetric AnalyzeTail(double trailingSilenceSeconds, double durationSeconds)
    {
        double score;
        if (trailingSilenceSeconds < 0.03)
            score = 0.42;
        else if (trailingSilenceSeconds <= 1.50)
            score = 0.92;
        else if (trailingSilenceSeconds <= Math.Max(2.5, durationSeconds * 0.20))
            score = 0.70;
        else
            score = 0.38;

        return new RenderMetric("tailCleanup", score,
            score >= 0.72
                ? $"Tail length is controlled with {trailingSilenceSeconds:0.00}s of trailing silence."
                : trailingSilenceSeconds < 0.03
                    ? "Render ends abruptly with almost no tail or release."
                    : $"Render leaves too much trailing silence at {trailingSilenceSeconds:0.00}s.");
    }

    private static RenderMetric AnalyzeLoopSeam(double seamDifference)
    {
        double score = Math.Clamp(1.0 - seamDifference * 4.0, 0.0, 1.0);
        return new RenderMetric("loopSeamContinuity", score,
            score >= 0.72
                ? "Opening and closing windows are close enough to support a smooth loop seam."
                : "Opening and closing windows differ enough that the loop seam may click or jump.");
    }

    private static RenderMetric AnalyzeStereo(double stereoDiffDb, int channels)
    {
        if (channels < 2)
            return new RenderMetric("stereoBalance", 0.80, "Render is mono, so stereo balance is not a concern.");

        double score = Math.Clamp(1.0 - stereoDiffDb / 6.0, 0.0, 1.0);
        return new RenderMetric("stereoBalance", score,
            score >= 0.72
                ? $"Stereo balance is controlled within {stereoDiffDb:0.0} dB."
                : $"Left/right energy differs by {stereoDiffDb:0.0} dB, which suggests an imbalanced stereo field.");
    }

    private static double ComputeSeamDifference(IReadOnlyList<float> firstWindow, IReadOnlyList<float> lastWindow, int channels)
    {
        int sampleCount = Math.Min(firstWindow.Count, lastWindow.Count);
        if (sampleCount == 0 || channels <= 0)
            return 0.5;

        double total = 0;
        for (int index = 0; index < sampleCount; index++)
            total += Math.Abs(firstWindow[index] - lastWindow[index]);

        return total / sampleCount;
    }
}
