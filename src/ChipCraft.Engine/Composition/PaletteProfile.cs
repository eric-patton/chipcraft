using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Midi;

namespace ChipCraft.Engine.Composition;

public record ChannelRoleAssignment(
    int Channel,
    ChannelRole Role,
    string ProgramName,
    float Volume,
    float Pan,
    bool IsDrumChannel = false
);

public record PaletteProfile(
    string Name,
    string Description,
    IReadOnlyList<ChannelRoleAssignment> Assignments
)
{
    public int ChannelCount => Assignments.Count;
}

public static class PaletteProfileLibrary
{
    public const string DefaultPaletteName = "broad-gm";

    public static PaletteProfile Resolve(string? name, Mood mood, Genre genre)
    {
        string normalized = string.IsNullOrWhiteSpace(name)
            ? DefaultPaletteName
            : name.Trim().ToLowerInvariant();

        return normalized switch
        {
            "bright" or "light" => CreateBrightProfile(genre),
            "dark" => CreateDarkProfile(genre),
            "ambient" => CreateAmbientProfile(mood, genre),
            "cinematic" or "orchestral" => CreateCinematicProfile(mood, genre),
            "retro" or "retro-console" or "8-bit" or "chiptune" => CreateRetroConsoleProfile(mood, genre),
            "boss" or "boss-battle" or "battle-boss" => CreateBossBattleProfile(mood, genre),
            "hybrid" or "hybrid-chip" or "chip-hybrid" => CreateHybridProfile(mood, genre),
            _ => CreateBroadGmProfile(mood, genre)
        };
    }

    private static PaletteProfile CreateBroadGmProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            DefaultPaletteName,
            "Broad General MIDI palette for game music cues.",
            [
                new(0, ChannelRole.Lead, SelectLeadProgram(mood, genre, "broad-gm"), 0.82f, -0.10f),
                new(1, ChannelRole.Bass, SelectBassProgram(mood, genre, "broad-gm"), 0.84f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.88f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, SelectHarmonyProgram(mood, genre), 0.62f, 0.18f),
                new(4, ChannelRole.PadLow, SelectPadProgram(mood, genre, "broad-gm", upper: false), 0.54f, -0.32f),
                new(5, ChannelRole.PadHigh, SelectPadProgram(mood, genre, "broad-gm", upper: true), 0.50f, 0.32f)
            ]);
    }

    private static PaletteProfile CreateBrightProfile(Genre genre)
    {
        return new PaletteProfile(
            "bright",
            "Brighter melodic profile with open leads and lighter support.",
            [
                new(0, ChannelRole.Lead, SelectLeadProgram(Mood.Calm, genre, "bright"), 0.84f, -0.15f),
                new(1, ChannelRole.Bass, "Acoustic Bass", 0.80f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.84f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "String Ensemble 1", 0.58f, 0.20f),
                new(4, ChannelRole.PadLow, "Pad 2 Warm", 0.50f, -0.28f),
                new(5, ChannelRole.PadHigh, "Choir Aahs", 0.46f, 0.28f)
            ]);
    }

    private static PaletteProfile CreateDarkProfile(Genre genre)
    {
        return new PaletteProfile(
            "dark",
            "Darker, moodier General MIDI palette.",
            [
                new(0, ChannelRole.Lead, SelectLeadProgram(Mood.Dark, genre, "dark"), 0.80f, -0.12f),
                new(1, ChannelRole.Bass, "Synth Bass 2", 0.86f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.90f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "Synth Strings 1", 0.60f, 0.16f),
                new(4, ChannelRole.PadLow, "Pad 6 Metallic", 0.52f, -0.28f),
                new(5, ChannelRole.PadHigh, "Pad 7 Halo", 0.48f, 0.28f)
            ]);
    }

    private static PaletteProfile CreateAmbientProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            "ambient",
            "Soft atmospheric palette for exploration, menus, and low-intensity cues.",
            [
                new(0, ChannelRole.Lead, mood is Mood.Mysterious or Mood.Dark ? "Oboe" : "Flute", 0.76f, -0.12f),
                new(1, ChannelRole.Bass, genre == Genre.Space ? "Synth Bass 1" : "Fretless Bass", 0.74f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.72f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "Voice Oohs", 0.56f, 0.18f),
                new(4, ChannelRole.PadLow, "Pad 2 Warm", 0.50f, -0.28f),
                new(5, ChannelRole.PadHigh, "Pad 8 Sweep", 0.46f, 0.28f)
            ]);
    }

    private static PaletteProfile CreateCinematicProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            "cinematic",
            "Broader orchestral-leaning GM palette for more dramatic arrangements.",
            [
                new(0, ChannelRole.Lead, mood is Mood.Triumphant or Mood.Epic ? "French Horn" : "Violin", 0.86f, -0.10f),
                new(1, ChannelRole.Bass, genre == Genre.Horror ? "Contrabass" : "Cello", 0.82f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.90f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "String Ensemble 1", 0.66f, 0.16f),
                new(4, ChannelRole.PadLow, "Slow Strings", 0.58f, -0.26f),
                new(5, ChannelRole.PadHigh, "Choir Aahs", 0.54f, 0.26f)
            ]);
    }

    private static PaletteProfile CreateRetroConsoleProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            "retro-console",
            "Sharper retro-forward palette with square leads and tighter synthetic backing.",
            [
                new(0, ChannelRole.Lead, "Lead 1 Square", 0.86f, -0.12f),
                new(1, ChannelRole.Bass, genre == Genre.Space ? "Synth Bass 1" : "Synth Bass 2", 0.84f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.86f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, mood == Mood.Playful ? "Lead 3 Calliope" : "Synth Strings 2", 0.56f, 0.18f),
                new(4, ChannelRole.PadLow, "Pad 3 Polysynth", 0.50f, -0.24f),
                new(5, ChannelRole.PadHigh, "Lead 7 Fifths", 0.48f, 0.24f)
            ]);
    }

    private static PaletteProfile CreateBossBattleProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            "boss-battle",
            "Aggressive GM palette with heavier bass and brighter attack for boss encounters.",
            [
                new(0, ChannelRole.Lead, genre == Genre.Space ? "Lead 2 Sawtooth" : "Trumpet", 0.88f, -0.10f),
                new(1, ChannelRole.Bass, "Synth Bass 2", 0.90f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.94f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "Brass Section", 0.68f, 0.18f),
                new(4, ChannelRole.PadLow, "Pad 6 Metallic", 0.54f, -0.24f),
                new(5, ChannelRole.PadHigh, "Synth Brass 1", 0.52f, 0.24f)
            ]);
    }

    private static PaletteProfile CreateHybridProfile(Mood mood, Genre genre)
    {
        return new PaletteProfile(
            "hybrid-chip",
            "Hybrid retro lead with fuller GM backing.",
            [
                new(0, ChannelRole.Lead, "Lead 1 Square", 0.84f, -0.10f),
                new(1, ChannelRole.Bass, SelectBassProgram(mood, genre, "hybrid"), 0.84f, 0.00f),
                new(2, ChannelRole.Drums, MidiProgram.Drums.Name, 0.88f, 0.00f, IsDrumChannel: true),
                new(3, ChannelRole.Harmony, "Synth Strings 2", 0.58f, 0.18f),
                new(4, ChannelRole.PadLow, "Pad 3 Polysynth", 0.52f, -0.26f),
                new(5, ChannelRole.PadHigh, "Lead 7 Fifths", 0.50f, 0.26f)
            ]);
    }

    private static string SelectLeadProgram(Mood mood, Genre genre, string palette) => (mood, genre, palette) switch
    {
        (_, Genre.RpgTown, "bright") => "Flute",
        (_, Genre.RpgTown, _) => "Ocarina",
        (_, Genre.Puzzle, _) => "Music Box",
        (_, Genre.Horror, _) => "English Horn",
        (_, Genre.Space, _) => "Lead 2 Sawtooth",
        (Mood.Calm, _, _) => "Flute",
        (Mood.Playful, _, _) => "Clarinet",
        (Mood.Mysterious, _, _) => "Lead 3 Calliope",
        (Mood.Dark, _, _) => "Lead 5 Charang",
        (Mood.Triumphant, _, _) => "Trumpet",
        (Mood.Epic, _, _) => "French Horn",
        (Mood.Urgent, _, _) => "Lead 2 Sawtooth",
        _ => "Lead 1 Square"
    };

    private static string SelectBassProgram(Mood mood, Genre genre, string palette) => (mood, genre, palette) switch
    {
        (_, Genre.RpgTown, _) => "Acoustic Bass",
        (_, Genre.Puzzle, _) => "Acoustic Bass",
        (_, Genre.Horror, _) => "Fretless Bass",
        (_, Genre.Space, _) => "Synth Bass 1",
        (Mood.Calm, _, _) => "Acoustic Bass",
        (Mood.Playful, _, _) => "Electric Bass Finger",
        (Mood.Dark, _, _) => "Synth Bass 2",
        _ => "Electric Bass Pick"
    };

    private static string SelectHarmonyProgram(Mood mood, Genre genre) => (mood, genre) switch
    {
        (_, Genre.RpgTown) => "String Ensemble 1",
        (_, Genre.Horror) => "Synth Strings 1",
        (_, Genre.Space) => "Synth Voice",
        (Mood.Triumphant, _) => "Brass Section",
        (Mood.Heroic, _) => "String Ensemble 1",
        (Mood.Epic, _) => "French Horn",
        (Mood.Melancholy, _) => "Cello",
        _ => "Synth Strings 1"
    };

    private static string SelectPadProgram(Mood mood, Genre genre, string palette, bool upper) => (mood, genre, palette, upper) switch
    {
        (_, Genre.RpgTown, _, false) => "Pad 2 Warm",
        (_, Genre.RpgTown, _, true) => "Choir Aahs",
        (_, Genre.Horror, _, false) => "Pad 6 Metallic",
        (_, Genre.Horror, _, true) => "Pad 7 Halo",
        (_, Genre.Space, _, false) => "Pad 3 Polysynth",
        (_, Genre.Space, _, true) => "Pad 8 Sweep",
        (Mood.Playful, _, _, false) => "Pad 1 New Age",
        (Mood.Playful, _, _, true) => "Voice Oohs",
        (Mood.Dark, _, _, false) => "Pad 6 Metallic",
        (Mood.Dark, _, _, true) => "Pad 7 Halo",
        (_, _, "bright", false) => "Pad 2 Warm",
        (_, _, "bright", true) => "Choir Aahs",
        _ when upper => "Synth Strings 2",
        _ => "Pad 2 Warm"
    };
}
