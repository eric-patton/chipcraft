namespace ChipCraft.Engine.Composition;

public record ArrangementSection(
    string Label,
    int StartBar,
    int Bars,
    string Function,
    float Intensity,
    string[] Chords,
    string MaterialKey,
    string? VariationOf = null
);

public record ArrangementPlan(
    int TotalBars,
    bool Loop,
    string Form,
    IReadOnlyList<ArrangementSection> Sections
);
