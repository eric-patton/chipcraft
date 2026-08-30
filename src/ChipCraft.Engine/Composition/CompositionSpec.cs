using ChipCraft.Engine.Generation;
using ChipCraft.Engine.Theory;

namespace ChipCraft.Engine.Composition;

public record CompositionSpec(
    string Title,
    string Prompt,
    Genre Genre,
    Mood Mood,
    int Bars,
    bool Loop,
    string KeyName,
    ScaleType ScaleType,
    int Tempo,
    string Palette,
    int? Seed,
    float Energy,
    string FormHint = "loop-variation",
    int RowsPerBeat = 4
)
{
    public Key ToKey()
    {
        var key = Key.Parse(KeyName);
        return key.ScaleType == ScaleType ? key : new Key(key.Root, ScaleType);
    }
}
