using System.CommandLine;
using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Cli.Commands;

public static class TheoryCommand
{
    public static Command Create()
    {
        var cmd = new Command("theory", "Music theory tools");
        cmd.AddCommand(CreateScaleCommand());
        cmd.AddCommand(CreateChordCommand());
        cmd.AddCommand(CreateProgressionCommand());
        return cmd;
    }

    private static Command CreateScaleCommand()
    {
        var rootArg = new Argument<string>("root", "Root note, e.g. C, D, F#");
        var typeArg = new Argument<string>("type", () => "NaturalMinor", "Scale type");

        var cmd = new Command("scale", "Show notes in a scale") { rootArg, typeArg };

        cmd.SetHandler((string root, string type) =>
        {
            var scaleType = Enum.Parse<ScaleType>(type, ignoreCase: true);
            var scale = new Scale(root, scaleType);
            Console.WriteLine($"{root} {scaleType}: {string.Join(" ", scale.GetNoteNames())}");
        }, rootArg, typeArg);

        return cmd;
    }

    private static Command CreateChordCommand()
    {
        var symbolArg = new Argument<string>("symbol", "Chord symbol, e.g. Am, C, G7, F#dim");

        var cmd = new Command("chord", "Show notes in a chord") { symbolArg };

        cmd.SetHandler((string symbol) =>
        {
            var chord = Chord.Parse(symbol);
            Console.WriteLine($"{chord}: {string.Join(" ", chord.GetNoteNames())}");
        }, symbolArg);

        return cmd;
    }

    private static Command CreateProgressionCommand()
    {
        var keyArg = new Argument<string>("key", "Musical key, e.g. Am, C, Dm");
        var moodOpt = new Option<string>("--mood", () => "Heroic", "Mood");
        var genreOpt = new Option<string>("--genre", () => "Action", "Genre");

        var cmd = new Command("progression", "Suggest chord progressions") { keyArg, moodOpt, genreOpt };

        cmd.SetHandler((string key, string mood, string genre) =>
        {
            var keyObj = Key.Parse(key);
            var moodEnum = Enum.Parse<Mood>(mood, ignoreCase: true);
            var genreEnum = Enum.Parse<Genre>(genre, ignoreCase: true);

            var generator = new ChordProgressionGenerator();
            var results = generator.GenerateMultiple(new ProgressionOptions(keyObj, moodEnum, genreEnum, Bars: 4), 3);

            foreach (var p in results)
            {
                Console.WriteLine($"  [{p.TemplateName}] {string.Join(" | ", p.Chords.Select(c => c.Chord.ToString()))}");
            }
        }, keyArg, moodOpt, genreOpt);

        return cmd;
    }
}
