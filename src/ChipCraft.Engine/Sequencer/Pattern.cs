using ChipCraft.Engine.Midi;

namespace ChipCraft.Engine.Sequencer;

/// <summary>
/// A 2D grid of rows x channels. The fundamental compositional unit.
/// Rows represent time steps; channels represent parallel voices.
/// </summary>
public class Pattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Pattern";
    public int RowCount { get; }
    public int ChannelCount { get; }
    public List<Part> Parts { get; } = [];

    private readonly PatternCell[,] _cells;

    public Pattern(int rowCount = 64, int channelCount = 4)
    {
        RowCount = rowCount;
        ChannelCount = channelCount;
        _cells = new PatternCell[rowCount, channelCount];

        for (int r = 0; r < rowCount; r++)
            for (int c = 0; c < channelCount; c++)
                _cells[r, c] = PatternCell.Empty;
    }

    public PatternCell GetCell(int row, int channel)
    {
        ValidateIndices(row, channel);
        return _cells[row, channel];
    }

    public void SetCell(int row, int channel, PatternCell cell)
    {
        ValidateIndices(row, channel);
        _cells[row, channel] = cell;
    }

    public void ClearCell(int row, int channel)
    {
        ValidateIndices(row, channel);
        _cells[row, channel] = PatternCell.Empty;
    }

    public Part CreatePart(int channel, string? name = null, bool isDrumPart = false)
    {
        ValidateIndices(0, channel);
        var part = new Part
        {
            Name = name ?? $"Part {Parts.Count + 1}",
            Channel = channel,
            IsDrumPart = isDrumPart
        };
        Parts.Add(part);
        return part;
    }

    public Part? GetPart(string partId) =>
        Parts.FirstOrDefault(part => part.Id.Equals(partId, StringComparison.Ordinal));

    /// <summary>
    /// Set a range of cells from a NoteSequence onto a specific channel.
    /// Converts beat-based timing to row-based timing using rowsPerBeat.
    /// </summary>
    public void ApplyNoteSequence(Generation.NoteSequence sequence, int channel, string instrumentId = "", int rowsPerBeat = 4)
    {
        foreach (var evt in sequence.Events)
        {
            if (evt.IsRest) continue;

            int row = (int)(evt.StartBeat * rowsPerBeat);
            if (row >= RowCount) break;

            byte vol = (byte)Math.Clamp((int)(evt.Velocity * 15), 0, 15);
            SetCell(row, channel, new PatternCell(evt.Note, instrumentId, vol));

            int endRow = (int)(evt.EndBeat * rowsPerBeat);
            if (endRow < RowCount && endRow > row)
            {
                SetCell(endRow, channel, new PatternCell(Models.Note.Cut));
            }
        }
    }

    /// <summary>
    /// Reconstruct note events for a single channel by scanning note starts and cut cells.
    /// Useful for analysis and for regenerating derived layers from an existing pattern.
    /// </summary>
    public Generation.NoteSequence ToNoteSequence(int channel, int rowsPerBeat = 4)
    {
        ValidateIndices(0, channel);

        var events = new List<Generation.NoteEvent>();
        int? activeStartRow = null;
        Models.Note? activeNote = null;
        byte? activeVolume = null;

        void Flush(int endRow)
        {
            if (activeStartRow.HasValue && activeNote.HasValue)
            {
                float startBeat = activeStartRow.Value / (float)rowsPerBeat;
                float duration = Math.Max(0.25f, (endRow - activeStartRow.Value) / (float)rowsPerBeat);
                float velocity = activeVolume.HasValue ? activeVolume.Value / 15f : 0.8f;
                events.Add(new Generation.NoteEvent(activeNote.Value, startBeat, duration, velocity));
            }

            activeStartRow = null;
            activeNote = null;
            activeVolume = null;
        }

        for (int row = 0; row < RowCount; row++)
        {
            var cell = GetCell(row, channel);
            if (!cell.Note.HasValue)
                continue;

            var note = cell.Note.Value;
            if (note.IsCut || note.IsRest)
            {
                Flush(row);
                continue;
            }

            if (activeNote.HasValue)
                Flush(row);

            activeStartRow = row;
            activeNote = note;
            activeVolume = cell.Volume;
        }

        if (activeNote.HasValue)
            Flush(RowCount);

        return new Generation.NoteSequence
        {
            Events = events,
            TotalBars = Math.Max(1, RowCount / (rowsPerBeat * 4)),
            BeatsPerBar = 4
        };
    }

    /// <summary>
    /// Apply drum hits to a channel using GM percussion note numbers.
    /// Each DrumVoice maps to a specific MIDI note on the GM drum channel.
    /// </summary>
    public void ApplyDrumPattern(Generation.DrumPattern drumPattern, int channel, int rowsPerBeat = 4)
    {
        foreach (var hit in drumPattern.Hits)
        {
            int row = (int)(hit.Beat * rowsPerBeat);
            if (row >= RowCount) continue;

            var note = Models.Note.FromMidi(GmDrumMap.GetMidiNote(hit.Voice));
            byte vol = (byte)Math.Clamp((int)(hit.Velocity * 15), 0, 15);
            SetCell(row, channel, new PatternCell(note, null, vol));

            // Auto-cut drums after 1-2 rows so they don't sustain
            int cutRow = row + (hit.Voice is Generation.DrumVoice.Crash or Generation.DrumVoice.HiHatOpen ? 3 : 1);
            if (cutRow < RowCount && GetCell(cutRow, channel).IsEmpty)
                SetCell(cutRow, channel, new PatternCell(Models.Note.Cut));
        }
    }

    private void ValidateIndices(int row, int channel)
    {
        if (row < 0 || row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} out of range (0-{RowCount - 1}).");
        if (channel < 0 || channel >= ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel {channel} out of range (0-{ChannelCount - 1}).");
    }
}
