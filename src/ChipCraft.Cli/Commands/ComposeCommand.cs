using System.CommandLine;
using System.CommandLine.Invocation;
using ChipCraft.Engine.Midi;

namespace ChipCraft.Cli.Commands;

public static class RenderCommand
{
    public static Command Create()
    {
        var inputOpt = new Option<string>("--input", "Input MIDI file path (.mid)") { IsRequired = true };
        inputOpt.AddAlias("-i");
        var outputOpt = new Option<string>("--output", "Output WAV file path") { IsRequired = true };
        outputOpt.AddAlias("-o");
        var soundfontOpt = new Option<string>("--soundfont", "Path to .sf2 soundfont file") { IsRequired = true };
        soundfontOpt.AddAlias("-sf");
        var sampleRateOpt = new Option<int>("--sample-rate", () => 44100, "Sample rate in Hz");

        var cmd = new Command("render", "Render a MIDI file to WAV using a SoundFont")
        {
            inputOpt, outputOpt, soundfontOpt, sampleRateOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            string input = ctx.ParseResult.GetValueForOption(inputOpt)!;
            string output = ctx.ParseResult.GetValueForOption(outputOpt)!;
            string soundfont = ctx.ParseResult.GetValueForOption(soundfontOpt)!;
            int sampleRate = ctx.ParseResult.GetValueForOption(sampleRateOpt);

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"MIDI file not found: {input}");
                ctx.ExitCode = 1;
                return;
            }
            if (!File.Exists(soundfont))
            {
                Console.Error.WriteLine($"SoundFont not found: {soundfont}");
                ctx.ExitCode = 1;
                return;
            }

            Console.WriteLine($"Rendering: {input} with {Path.GetFileName(soundfont)}");

            var renderer = new AudioRenderer(soundfont, sampleRate);
            renderer.RenderMidiToWav(input, output);

            Console.WriteLine($"Rendered -> {output} ({new FileInfo(output).Length:N0} bytes)");
        });

        return cmd;
    }
}
