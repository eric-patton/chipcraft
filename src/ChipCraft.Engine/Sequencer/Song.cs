using ChipCraft.Engine.Midi;

namespace ChipCraft.Engine.Sequencer;

public record OrderEntry(int PatternIndex, int? TempoOverride = null);

public class OrderList
{
    public List<OrderEntry> Entries { get; set; } = [];

    /// <summary>
    /// Index into Entries where the song loops back to. Null = play once, no loop.
    /// </summary>
    public int? LoopStartIndex { get; set; }
}

/// <summary>
/// Complete song: metadata + MIDI program assignments + patterns + play order.
/// This is the top-level composition document.
/// </summary>
public class Song
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "Untitled";
    public string? Author { get; set; }
    public string? KeyName { get; set; }

    // Tempo
    public int Tempo { get; set; } = 120;
    public int RowsPerBeat { get; set; } = 4;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;

    // Channel configuration
    public int ChannelCount { get; set; } = 4;
    public float[] ChannelVolumes { get; set; } = [];
    public float[] ChannelPans { get; set; } = [];
    public byte[] ChannelReverbSends { get; set; } = [];
    public byte[] ChannelChorusSends { get; set; } = [];
    public bool[] ChannelMutes { get; set; } = [];
    public bool[] ChannelSolos { get; set; } = [];

    // Content
    public List<Pattern> Patterns { get; set; } = [];
    public OrderList OrderList { get; set; } = new();

    // MIDI program assignments (channel index → GM program)
    public Dictionary<int, MidiProgram> ChannelPrograms { get; set; } = [];
    public HashSet<int> DrumChannels { get; set; } = [];

    // Master
    public float MasterVolume { get; set; } = 0.8f;

    public void InitializeChannels(int channelCount)
    {
        ChannelCount = channelCount;
        ChannelVolumes = new float[channelCount];
        ChannelPans = new float[channelCount];
        ChannelReverbSends = new byte[channelCount];
        ChannelChorusSends = new byte[channelCount];
        ChannelMutes = new bool[channelCount];
        ChannelSolos = new bool[channelCount];
        Array.Fill(ChannelVolumes, 0.75f);
        Array.Fill(ChannelPans, 0f);
        Array.Fill(ChannelReverbSends, (byte)40);
        Array.Fill(ChannelChorusSends, (byte)0);
    }

    public void SetChannelProgram(int channel, MidiProgram program)
    {
        ValidateChannel(channel);
        ChannelPrograms[channel] = program;
    }

    public void SetDrumChannel(int channel)
    {
        ValidateChannel(channel);
        DrumChannels.Add(channel);
        ChannelPrograms[channel] = MidiProgram.Drums;
    }

    public int AddPattern(Pattern pattern)
    {
        Patterns.Add(pattern);
        return Patterns.Count - 1;
    }

    public void AddToOrder(int patternIndex)
    {
        OrderList.Entries.Add(new OrderEntry(patternIndex));
    }

    public int TotalRows => OrderList.Entries.Sum(e => Patterns[e.PatternIndex].RowCount);

    public bool HasSoloChannels => ChannelSolos.Any(flag => flag);

    public bool IsChannelAudible(int channel)
    {
        ValidateChannel(channel);
        if (ChannelMutes.Length > channel && ChannelMutes[channel])
            return false;

        if (!HasSoloChannels)
            return true;

        return ChannelSolos.Length > channel && ChannelSolos[channel];
    }

    public double TotalDurationSeconds
    {
        get
        {
            if (OrderList.Entries.Count == 0)
                return 0;

            double totalSeconds = 0;
            int currentTempo = Tempo;
            foreach (var entry in OrderList.Entries)
            {
                if (entry.PatternIndex < 0 || entry.PatternIndex >= Patterns.Count)
                    continue;

                if (entry.TempoOverride.HasValue)
                    currentTempo = entry.TempoOverride.Value;

                double secondsPerRow = 60.0 / Math.Max(1, currentTempo) / RowsPerBeat;
                totalSeconds += Patterns[entry.PatternIndex].RowCount * secondsPerRow;
            }

            return totalSeconds;
        }
    }

    private void ValidateChannel(int channel)
    {
        if (channel < 0 || channel >= ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel {channel} out of range (0-{ChannelCount - 1}).");
    }
}
