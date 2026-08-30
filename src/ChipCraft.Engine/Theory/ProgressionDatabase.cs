using ChipCraft.Engine.Generation;

namespace ChipCraft.Engine.Theory;

/// <summary>
/// Database of chord progressions tagged by mood and genre.
/// Progressions are stored as roman numeral degree sequences that get
/// realized into actual chords by the ChordProgressionGenerator.
/// </summary>
public static class ProgressionDatabase
{
    /// <summary>
    /// A named progression template with mood/genre tags and a weight score.
    /// Degrees are 1-indexed scale degrees. Negative means "flat" (e.g., -7 = bVII).
    /// Quality overrides: 0 = diatonic default, 1 = force major, -1 = force minor.
    /// </summary>
    public record ProgressionTemplate(
        string Name,
        ProgressionDegree[] Degrees,
        Mood[] Moods,
        Genre[] Genres,
        bool PreferMinor = false,
        bool PreferMajor = false
    );

    public record ProgressionDegree(int Degree, int QualityOverride = 0)
    {
        /// <summary>
        /// Flat degree (e.g., bVII = degree 7 lowered by 1 semitone).
        /// </summary>
        public bool IsFlat => Degree < 0;
        public int AbsDegree => Math.Abs(Degree);
    }

    private static ProgressionDegree D(int degree, int quality = 0) => new(degree, quality);

    public static IReadOnlyList<ProgressionTemplate> AllProgressions { get; } =
    [
        // ── Action / Heroic ──────────────────────────────────────
        new("Epic Minor",
            [D(1), D(-7), D(-6), D(-7)],
            [Mood.Heroic, Mood.Epic, Mood.Urgent],
            [Genre.Action, Genre.RpgBattle, Genre.Sports],
            PreferMinor: true),

        new("Power Drive",
            [D(1), D(-7), D(4), D(1)],
            [Mood.Heroic, Mood.Epic],
            [Genre.Action, Genre.Platformer, Genre.Sports],
            PreferMajor: true),

        new("Battle Surge",
            [D(1), D(4, -1), D(-6), D(-7)],
            [Mood.Heroic, Mood.Urgent, Mood.Tense],
            [Genre.RpgBattle, Genre.Action],
            PreferMinor: true),

        new("Relentless March",
            [D(1), D(-3), D(-7), D(4)],
            [Mood.Urgent, Mood.Epic, Mood.Dark],
            [Genre.Action, Genre.RpgBattle],
            PreferMinor: true),

        // ── Tense / Dark ─────────────────────────────────────────
        new("Andalusian Cadence",
            [D(1), D(-7), D(-6), D(5, 1)],
            [Mood.Mysterious, Mood.Dark, Mood.Tense],
            [Genre.Fantasy, Genre.Horror, Genre.Action],
            PreferMinor: true),

        new("Chromatic Descent",
            [D(1), D(-2, 1), D(1), D(5, 1)],
            [Mood.Tense, Mood.Dark, Mood.Mysterious],
            [Genre.Horror, Genre.Fantasy],
            PreferMinor: true),

        new("Doom Loop",
            [D(1), D(-2, 1), D(-7), D(1)],
            [Mood.Dark, Mood.Tense],
            [Genre.Horror, Genre.Space],
            PreferMinor: true),

        new("Suspense Build",
            [D(1), D(1), D(-6), D(5, 1)],
            [Mood.Tense, Mood.Mysterious],
            [Genre.Horror, Genre.Space, Genre.Fantasy],
            PreferMinor: true),

        // ── Calm / Peaceful ──────────────────────────────────────
        new("Gentle Village",
            [D(1), D(6, -1), D(4), D(5)],
            [Mood.Calm, Mood.Playful],
            [Genre.RpgTown, Genre.Puzzle, Genre.Fantasy],
            PreferMajor: true),

        new("Pastoral",
            [D(1), D(4), D(5), D(1)],
            [Mood.Calm, Mood.Triumphant, Mood.Playful],
            [Genre.RpgTown, Genre.Platformer, Genre.Fantasy],
            PreferMajor: true),

        new("Wistful Reflection",
            [D(1), D(3, -1), D(4), D(4, -1)],
            [Mood.Melancholy, Mood.Calm],
            [Genre.RpgTown, Genre.Fantasy, Genre.Puzzle],
            PreferMajor: true),

        new("Morning Stroll",
            [D(1), D(5), D(6, -1), D(4)],
            [Mood.Calm, Mood.Playful],
            [Genre.Platformer, Genre.RpgTown, Genre.Puzzle],
            PreferMajor: true),

        // ── Triumphant ───────────────────────────────────────────
        new("Victory Fanfare",
            [D(1), D(4), D(1), D(5)],
            [Mood.Triumphant, Mood.Heroic, Mood.Epic],
            [Genre.Action, Genre.RpgBattle, Genre.Sports],
            PreferMajor: true),

        new("Hero's Return",
            [D(1), D(5), D(4), D(1)],
            [Mood.Triumphant, Mood.Heroic],
            [Genre.Fantasy, Genre.Action, Genre.Sports],
            PreferMajor: true),

        // ── Melancholy ───────────────────────────────────────────
        new("Sad Descent",
            [D(1), D(-7), D(-6), D(-5)],
            [Mood.Melancholy, Mood.Dark],
            [Genre.RpgTown, Genre.Fantasy],
            PreferMinor: true),

        new("Bittersweet",
            [D(1), D(3), D(4), D(4, -1)],
            [Mood.Melancholy, Mood.Calm],
            [Genre.RpgTown, Genre.Fantasy, Genre.Puzzle],
            PreferMajor: true),

        new("Lonely Path",
            [D(4, -1), D(1), D(5, 1), D(1)],
            [Mood.Melancholy, Mood.Mysterious],
            [Genre.Fantasy, Genre.Space],
            PreferMinor: true),

        // ── Playful ──────────────────────────────────────────────
        new("Bouncy Theme",
            [D(1), D(4), D(5), D(4)],
            [Mood.Playful, Mood.Calm],
            [Genre.Platformer, Genre.Puzzle, Genre.RpgTown],
            PreferMajor: true),

        new("Mischief",
            [D(1), D(-7), D(4), D(-7)],
            [Mood.Playful, Mood.Mysterious],
            [Genre.Platformer, Genre.Puzzle],
            PreferMajor: true),

        // ── Mysterious / Space ───────────────────────────────────
        new("Alien Drift",
            [D(1), D(-2, 1), D(4), D(1)],
            [Mood.Mysterious, Mood.Dark],
            [Genre.Space, Genre.Horror, Genre.Fantasy],
            PreferMinor: true),

        new("Cosmic Wonder",
            [D(1), D(-3), D(-6), D(-7)],
            [Mood.Mysterious, Mood.Epic],
            [Genre.Space, Genre.Fantasy],
            PreferMinor: true),
    ];

    /// <summary>
    /// Score a progression template against a mood and genre.
    /// Higher score = better fit.
    /// </summary>
    public static double ScoreProgression(ProgressionTemplate template, Mood mood, Genre genre)
    {
        double score = 0;

        if (template.Moods.Contains(mood))
            score += 10;
        if (template.Genres.Contains(genre))
            score += 8;

        // Partial credit for related moods
        if (!template.Moods.Contains(mood))
        {
            foreach (var tmood in template.Moods)
            {
                if (AreRelatedMoods(mood, tmood))
                    score += 3;
            }
        }

        return score;
    }

    private static bool AreRelatedMoods(Mood a, Mood b) => (a, b) switch
    {
        (Mood.Heroic, Mood.Epic) or (Mood.Epic, Mood.Heroic) => true,
        (Mood.Heroic, Mood.Triumphant) or (Mood.Triumphant, Mood.Heroic) => true,
        (Mood.Tense, Mood.Dark) or (Mood.Dark, Mood.Tense) => true,
        (Mood.Tense, Mood.Urgent) or (Mood.Urgent, Mood.Tense) => true,
        (Mood.Calm, Mood.Playful) or (Mood.Playful, Mood.Calm) => true,
        (Mood.Calm, Mood.Melancholy) or (Mood.Melancholy, Mood.Calm) => true,
        (Mood.Mysterious, Mood.Dark) or (Mood.Dark, Mood.Mysterious) => true,
        _ => false
    };

    /// <summary>
    /// Default tempo for a genre (BPM).
    /// </summary>
    public static int GetDefaultTempo(Genre genre) => genre switch
    {
        Genre.Action => 150,
        Genre.RpgBattle => 160,
        Genre.RpgTown => 100,
        Genre.Platformer => 140,
        Genre.Puzzle => 110,
        Genre.Horror => 90,
        Genre.Space => 100,
        Genre.Fantasy => 120,
        Genre.Sports => 155,
        _ => 120
    };

    /// <summary>
    /// Default scale type for a mood.
    /// </summary>
    public static ScaleType GetDefaultScaleType(Mood mood) => mood switch
    {
        Mood.Heroic => ScaleType.NaturalMinor,
        Mood.Tense => ScaleType.HarmonicMinor,
        Mood.Calm => ScaleType.Major,
        Mood.Mysterious => ScaleType.Dorian,
        Mood.Triumphant => ScaleType.Major,
        Mood.Melancholy => ScaleType.NaturalMinor,
        Mood.Urgent => ScaleType.NaturalMinor,
        Mood.Playful => ScaleType.Mixolydian,
        Mood.Dark => ScaleType.Phrygian,
        Mood.Epic => ScaleType.NaturalMinor,
        _ => ScaleType.NaturalMinor
    };
}
