using System.Text.Json;
using System.Text.Json.Serialization;
using ChipCraft.Engine.Midi;
using ChipCraft.Engine.Models;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Engine.Persistence;

/// <summary>
/// Saves and loads Song objects as JSON files.
/// Pattern cell data is serialized as a sparse format (only non-empty cells).
/// </summary>
public static class SongSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new OrderEntryDtoConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(Song song)
    {
        var dto = SongDto.FromSong(song);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static Song Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<SongDto>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize song.");
        return dto.ToSong();
    }

    public static async Task SaveAsync(Song song, string filePath)
    {
        var json = Serialize(song);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task<Song> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json);
    }
}

file class SongDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? KeyName { get; set; }
    public int Tempo { get; set; }
    public int? Speed { get; set; }
    public int RowsPerBeat { get; set; }
    public int BeatsPerBar { get; set; }
    public int BeatUnit { get; set; }
    public int ChannelCount { get; set; }
    public float[] ChannelVolumes { get; set; } = [];
    public float[] ChannelPans { get; set; } = [];
    public byte[] ChannelReverbSends { get; set; } = [];
    public byte[] ChannelChorusSends { get; set; } = [];
    public bool[] ChannelMutes { get; set; } = [];
    public bool[] ChannelSolos { get; set; } = [];
    public float MasterVolume { get; set; }
    public Dictionary<int, MidiProgram> ChannelPrograms { get; set; } = [];
    public HashSet<int> DrumChannels { get; set; } = [];
    public List<PatternDto> Patterns { get; set; } = [];
    public List<OrderEntryDto> OrderList { get; set; } = [];
    public int? LoopStartIndex { get; set; }

    public static SongDto FromSong(Song song) => new()
    {
        Id = song.Id,
        Title = song.Title,
        Author = song.Author,
        KeyName = song.KeyName,
        Tempo = song.Tempo,
        RowsPerBeat = song.RowsPerBeat,
        BeatsPerBar = song.BeatsPerBar,
        BeatUnit = song.BeatUnit,
        ChannelCount = song.ChannelCount,
        ChannelVolumes = song.ChannelVolumes,
        ChannelPans = song.ChannelPans,
        ChannelReverbSends = song.ChannelReverbSends,
        ChannelChorusSends = song.ChannelChorusSends,
        ChannelMutes = song.ChannelMutes,
        ChannelSolos = song.ChannelSolos,
        MasterVolume = song.MasterVolume,
        ChannelPrograms = song.ChannelPrograms,
        DrumChannels = song.DrumChannels,
        Patterns = song.Patterns.Select(PatternDto.FromPattern).ToList(),
        OrderList = song.OrderList.Entries.Select(OrderEntryDto.FromOrderEntry).ToList(),
        LoopStartIndex = song.OrderList.LoopStartIndex
    };

    public Song ToSong()
    {
        int channelCount = Math.Max(1, ChannelCount);
        var song = new Song
        {
            Id = Id,
            Title = Title,
            Author = Author,
            KeyName = KeyName,
            Tempo = Tempo,
            RowsPerBeat = RowsPerBeat > 0 ? RowsPerBeat : 4,
            BeatsPerBar = BeatsPerBar > 0 ? BeatsPerBar : 4,
            BeatUnit = IsSupportedBeatUnit(BeatUnit) ? BeatUnit : 4,
            MasterVolume = MasterVolume,
            ChannelPrograms = ChannelPrograms,
            DrumChannels = DrumChannels,
            Patterns = Patterns.Select(p => p.ToPattern()).ToList(),
            OrderList = new OrderList
            {
                Entries = OrderList.Select(entry => entry.ToOrderEntry()).ToList(),
                LoopStartIndex = LoopStartIndex
            }
        };

        song.InitializeChannels(channelCount);
        song.ChannelVolumes = ResizeArray(ChannelVolumes, channelCount, 0.75f);
        song.ChannelPans = ResizeArray(ChannelPans, channelCount, 0f);
        song.ChannelReverbSends = ResizeArray(ChannelReverbSends, channelCount, (byte)40);
        song.ChannelChorusSends = ResizeArray(ChannelChorusSends, channelCount, (byte)0);
        song.ChannelMutes = ResizeArray(ChannelMutes, channelCount, false);
        song.ChannelSolos = ResizeArray(ChannelSolos, channelCount, false);
        return song;
    }

    private static bool IsSupportedBeatUnit(int beatUnit) =>
        beatUnit is 1 or 2 or 4 or 8 or 16 or 32;

    private static T[] ResizeArray<T>(T[]? source, int length, T fallback)
    {
        var values = new T[length];
        for (int index = 0; index < length; index++)
            values[index] = source != null && index < source.Length ? source[index] : fallback;
        return values;
    }
}

file class OrderEntryDto
{
    public int PatternIndex { get; set; }
    public int? TempoOverride { get; set; }
    public int? SpeedOverride { get; set; }

    public static OrderEntryDto FromOrderEntry(OrderEntry entry) => new()
    {
        PatternIndex = entry.PatternIndex,
        TempoOverride = entry.TempoOverride
    };

    public OrderEntry ToOrderEntry() => new(PatternIndex, TempoOverride);
}

file class OrderEntryDtoConverter : JsonConverter<OrderEntryDto>
{
    public override OrderEntryDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return new OrderEntryDto
            {
                PatternIndex = reader.GetInt32()
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Unexpected token {reader.TokenType} while reading order entry.");

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new OrderEntryDto
        {
            PatternIndex = root.GetProperty("PatternIndex").GetInt32(),
            TempoOverride = root.TryGetProperty("TempoOverride", out var tempo) && tempo.ValueKind != JsonValueKind.Null
                ? tempo.GetInt32()
                : null,
            SpeedOverride = root.TryGetProperty("SpeedOverride", out var speed) && speed.ValueKind != JsonValueKind.Null
                ? speed.GetInt32()
                : null
        };
    }

    public override void Write(Utf8JsonWriter writer, OrderEntryDto value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("PatternIndex", value.PatternIndex);
        if (value.TempoOverride.HasValue)
            writer.WriteNumber("TempoOverride", value.TempoOverride.Value);
        writer.WriteEndObject();
    }
}

file class PatternDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int RowCount { get; set; }
    public int ChannelCount { get; set; }
    public List<CellDto> Cells { get; set; } = [];
    public List<PartDto> Parts { get; set; } = [];

    public static PatternDto FromPattern(Pattern pattern)
    {
        var cells = new List<CellDto>();
        for (int r = 0; r < pattern.RowCount; r++)
            for (int c = 0; c < pattern.ChannelCount; c++)
            {
                var cell = pattern.GetCell(r, c);
                if (!cell.IsEmpty)
                    cells.Add(new CellDto
                    {
                        Row = r, Channel = c,
                        Note = cell.Note?.ToString(),
                        InstrumentId = cell.InstrumentId,
                        Volume = cell.Volume,
                        Effect = cell.Effect is { IsEmpty: false } ? cell.Effect.Type.ToString() : null,
                        EffectX = cell.Effect is { IsEmpty: false } ? cell.Effect.ParamX : null,
                        EffectY = cell.Effect is { IsEmpty: false } ? cell.Effect.ParamY : null
                    });
            }

        return new PatternDto
        {
            Id = pattern.Id, Name = pattern.Name,
            RowCount = pattern.RowCount, ChannelCount = pattern.ChannelCount,
            Cells = cells,
            Parts = pattern.Parts.Select(PartDto.FromPart).ToList()
        };
    }

    public Pattern ToPattern()
    {
        var pattern = new Pattern(RowCount, ChannelCount) { Id = Id, Name = Name };
        foreach (var cell in Cells)
        {
            var note = cell.Note != null ? Note.Parse(cell.Note) : (Note?)null;
            EffectCommand? effect = null;
            if (!string.IsNullOrWhiteSpace(cell.Effect)
                && Enum.TryParse<EffectCommandType>(cell.Effect, ignoreCase: true, out var effectType)
                && effectType != EffectCommandType.None)
            {
                effect = new EffectCommand(effectType, cell.EffectX ?? 0, cell.EffectY ?? 0);
            }

            pattern.SetCell(cell.Row, cell.Channel, new PatternCell(note, cell.InstrumentId, cell.Volume, effect));
        }

        foreach (var part in Parts)
            pattern.Parts.Add(part.ToPart());

        return pattern;
    }
}

file class CellDto
{
    public int Row { get; set; }
    public int Channel { get; set; }
    public string? Note { get; set; }
    public string? InstrumentId { get; set; }
    public byte? Volume { get; set; }
    public string? Effect { get; set; }
    public byte? EffectX { get; set; }
    public byte? EffectY { get; set; }
}

file class PartDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Channel { get; set; }
    public bool IsDrumPart { get; set; }
    public MidiProgram? ProgramOverride { get; set; }
    public List<PartNoteDto> Notes { get; set; } = [];
    public List<AutomationLaneDto> AutomationLanes { get; set; } = [];

    public static PartDto FromPart(Part part) => new()
    {
        Id = part.Id,
        Name = part.Name,
        Channel = part.Channel,
        IsDrumPart = part.IsDrumPart,
        ProgramOverride = part.ProgramOverride,
        Notes = part.Notes.Select(PartNoteDto.FromPartNote).ToList(),
        AutomationLanes = part.AutomationLanes.Select(AutomationLaneDto.FromLane).ToList()
    };

    public Part ToPart() => new()
    {
        Id = Id,
        Name = Name,
        Channel = Channel,
        IsDrumPart = IsDrumPart,
        ProgramOverride = ProgramOverride,
        Notes = Notes.Select(note => note.ToPartNote()).ToList(),
        AutomationLanes = AutomationLanes.Select(lane => lane.ToLane()).ToList()
    };
}

file class PartNoteDto
{
    public string Note { get; set; } = "";
    public float StartBeat { get; set; }
    public float DurationBeats { get; set; }
    public byte Velocity { get; set; }

    public static PartNoteDto FromPartNote(PartNote note) => new()
    {
        Note = note.Note.ToString(),
        StartBeat = note.StartBeat,
        DurationBeats = note.DurationBeats,
        Velocity = note.Velocity
    };

    public PartNote ToPartNote() => new(ChipCraft.Engine.Models.Note.Parse(Note), StartBeat, DurationBeats, Velocity);
}

file class AutomationLaneDto
{
    public AutomationLaneType Type { get; set; }
    public List<AutomationPointDto> Points { get; set; } = [];

    public static AutomationLaneDto FromLane(AutomationLane lane) => new()
    {
        Type = lane.Type,
        Points = lane.Points.Select(point => new AutomationPointDto
        {
            Beat = point.Beat,
            Value = point.Value
        }).ToList()
    };

    public AutomationLane ToLane() => new()
    {
        Type = Type,
        Points = Points.Select(point => new AutomationPoint(point.Beat, point.Value)).ToList()
    };
}

file class AutomationPointDto
{
    public float Beat { get; set; }
    public float Value { get; set; }
}
