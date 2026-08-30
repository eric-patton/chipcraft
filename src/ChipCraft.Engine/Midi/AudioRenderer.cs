using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Sequencer;
using MeltySynth;
using NAudio.Wave;

namespace ChipCraft.Engine.Midi;

/// <summary>
/// Renders a Song to audio (WAV) using a SoundFont (.sf2) for instrument sounds.
/// Pipeline: Song → MIDI events → MeltySynth synthesizer → WAV file.
/// </summary>
public class AudioRenderer
{
    private readonly string _soundFontPath;
    private readonly int _sampleRate;

    public AudioRenderer(string soundFontPath, int sampleRate = 44100)
    {
        if (!File.Exists(soundFontPath))
            throw new FileNotFoundException($"SoundFont not found: {soundFontPath}");
        _soundFontPath = soundFontPath;
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Render a Song to a WAV file using the loaded SoundFont.
    /// </summary>
    public void RenderToWav(Song song, string outputPath, SongProjectMetadata? metadata = null)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Step 1: Export song to a temporary MIDI file
        var midiExporter = new MidiExporter();
        var tempMidiPath = Path.Combine(Path.GetTempPath(), $"chipcraft_{Guid.NewGuid():N}.mid");

        try
        {
            midiExporter.Export(song, tempMidiPath, metadata);

            // Step 2: Load soundfont and MIDI
            var soundFont = new SoundFont(_soundFontPath);
            var midiFile = new MeltySynth.MidiFile(tempMidiPath);
            var synthesizer = new Synthesizer(soundFont, _sampleRate);
            var sequencer = new MidiFileSequencer(synthesizer);

            // Step 3: Render to audio buffer
            int totalSamples = (int)(_sampleRate * midiFile.Length.TotalSeconds) + _sampleRate; // +1s padding
            var leftBuffer = new float[totalSamples];
            var rightBuffer = new float[totalSamples];

            sequencer.Play(midiFile, false);
            sequencer.Render(leftBuffer, rightBuffer);

            // Step 4: Write interleaved stereo WAV
            var waveFormat = new WaveFormat(_sampleRate, 16, 2);
            using var writer = new WaveFileWriter(outputPath, waveFormat);

            for (int i = 0; i < totalSamples; i++)
            {
                // Clamp to [-1, 1] and write as 16-bit samples
                writer.WriteSample(Math.Clamp(leftBuffer[i], -1f, 1f));
                writer.WriteSample(Math.Clamp(rightBuffer[i], -1f, 1f));
            }
        }
        finally
        {
            if (File.Exists(tempMidiPath))
                File.Delete(tempMidiPath);
        }
    }

    /// <summary>
    /// Render a MIDI file directly to WAV.
    /// </summary>
    public void RenderMidiToWav(string midiPath, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var soundFont = new SoundFont(_soundFontPath);
        var midiFile = new MeltySynth.MidiFile(midiPath);
        var synthesizer = new Synthesizer(soundFont, _sampleRate);
        var sequencer = new MidiFileSequencer(synthesizer);

        int totalSamples = (int)(_sampleRate * midiFile.Length.TotalSeconds) + _sampleRate;
        var leftBuffer = new float[totalSamples];
        var rightBuffer = new float[totalSamples];

        sequencer.Play(midiFile, false);
        sequencer.Render(leftBuffer, rightBuffer);

        var waveFormat = new WaveFormat(_sampleRate, 16, 2);
        using var writer = new WaveFileWriter(outputPath, waveFormat);
        for (int i = 0; i < totalSamples; i++)
        {
            writer.WriteSample(Math.Clamp(leftBuffer[i], -1f, 1f));
            writer.WriteSample(Math.Clamp(rightBuffer[i], -1f, 1f));
        }
    }

    /// <summary>
    /// Render a Song to a WAV byte array.
    /// </summary>
    public byte[] RenderToBytes(Song song, SongProjectMetadata? metadata = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"chipcraft_{Guid.NewGuid():N}.wav");
        try
        {
            RenderToWav(song, tempPath, metadata);
            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
