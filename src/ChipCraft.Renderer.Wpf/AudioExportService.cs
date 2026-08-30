using System.IO;
using ChipCraft.Engine.Midi;
using NAudio.Lame;
using NAudio.Wave;

namespace ChipCraft.Renderer.Wpf;

public sealed class AudioExportService
{
    public void Render(string midiPath, string soundFontPath, string outputPath, AudioExportFormat format, int sampleRate)
    {
        if (!File.Exists(midiPath))
            throw new FileNotFoundException($"MIDI file not found: {midiPath}");
        if (!File.Exists(soundFontPath))
            throw new FileNotFoundException($"SoundFont not found: {soundFontPath}");

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var renderer = new AudioRenderer(soundFontPath, sampleRate);
        if (format == AudioExportFormat.Wav)
        {
            renderer.RenderMidiToWav(midiPath, outputPath);
            return;
        }

        string tempWavPath = Path.Combine(Path.GetTempPath(), $"chipcraft_render_{Guid.NewGuid():N}.wav");
        try
        {
            renderer.RenderMidiToWav(midiPath, tempWavPath);
            using var reader = new AudioFileReader(tempWavPath);
            using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, LAMEPreset.VBR_90);
            reader.CopyTo(writer);
        }
        finally
        {
            if (File.Exists(tempWavPath))
                File.Delete(tempWavPath);
        }
    }
}
