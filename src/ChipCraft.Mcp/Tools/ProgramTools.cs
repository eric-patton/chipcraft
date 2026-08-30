using System.ComponentModel;
using System.Text.Json;
using ChipCraft.Engine.Midi;
using ChipCraft.Mcp.State;
using ModelContextProtocol.Server;

namespace ChipCraft.Mcp.Tools;

[McpServerToolType]
public static class ProgramTools
{
    [McpServerTool(Name = "set_channel_program"), Description("Assign a General MIDI program to a song channel. Use list_gm_programs to browse available programs.")]
    public static string SetChannelProgram(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Channel index (0-based).")] int channel,
        [Description("GM program number (0-127) or program name (e.g. 'Flute', 'Electric Bass').")] string program)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!IsValidChannel(song.ChannelCount, channel))
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{song.ChannelCount - 1})." });

        MidiProgram? midiProgram = ResolveProgram(program);
        if (midiProgram == null)
            return JsonSerializer.Serialize(new { error = $"Program '{program}' not found. Use list_gm_programs to see available programs." });

        ApplyPatch(song, channel, midiProgram);
        return JsonSerializer.Serialize(new
        {
            songId,
            channel,
            programNumber = midiProgram.ProgramNumber,
            programName = midiProgram.Name,
            category = midiProgram.Category,
            bankMsb = midiProgram.BankMsb,
            bankLsb = midiProgram.BankLsb
        });
    }

    [McpServerTool(Name = "set_channel_patch"), Description("Assign a soundfont-aware patch binding to a song channel. This extends GM program selection with optional bank select values.")]
    public static string SetChannelPatch(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Channel index (0-based).")] int channel,
        [Description("GM program number (0-127) or program name used as the patch base.")] string program,
        [Description("Optional bank MSB value (0-127).")] int? bankMsb = null,
        [Description("Optional bank LSB value (0-127).")] int? bankLsb = null,
        [Description("Optional display name override for this patch binding.")] string? name = null)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!IsValidChannel(song.ChannelCount, channel))
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{song.ChannelCount - 1})." });

        MidiProgram? baseProgram = ResolveProgram(program);
        if (baseProgram == null)
            return JsonSerializer.Serialize(new { error = $"Program '{program}' not found. Use list_gm_programs to see available programs." });

        var patch = baseProgram with
        {
            Name = string.IsNullOrWhiteSpace(name) ? baseProgram.Name : name,
            BankMsb = (byte)Math.Clamp(bankMsb ?? baseProgram.BankMsb, 0, 127),
            BankLsb = (byte)Math.Clamp(bankLsb ?? baseProgram.BankLsb, 0, 127)
        };

        ApplyPatch(song, channel, patch);
        return JsonSerializer.Serialize(new
        {
            songId,
            channel,
            programNumber = patch.ProgramNumber,
            patchName = patch.Name,
            category = patch.Category,
            bankMsb = patch.BankMsb,
            bankLsb = patch.BankLsb
        });
    }

    [McpServerTool(Name = "set_drum_channel"), Description("Designate a channel as a GM percussion/drum channel (MIDI channel 10).")]
    public static string SetDrumChannel(
        SessionState session,
        [Description("Song ID.")] string songId,
        [Description("Channel index to designate as drums.")] int channel)
    {
        var song = session.GetSong(songId);
        if (song == null)
            return JsonSerializer.Serialize(new { error = $"Song '{songId}' not found." });
        if (!IsValidChannel(song.ChannelCount, channel))
            return JsonSerializer.Serialize(new { error = $"Channel {channel} out of range (0-{song.ChannelCount - 1})." });

        song.SetDrumChannel(channel);
        return JsonSerializer.Serialize(new { songId, channel, isDrumChannel = true });
    }

    [McpServerTool(Name = "list_gm_programs"), Description("List available General MIDI programs, optionally filtered by category (Piano, Bass, Strings, Brass, Synth Lead, etc.).")]
    public static string ListGmPrograms(
        [Description("Optional category filter: Piano, Chromatic Percussion, Organ, Guitar, Bass, Strings, Ensemble, Brass, Reed, Pipe, Synth Lead, Synth Pad, Synth Effects, Ethnic, Percussive, Sound Effects.")] string? category = null)
    {
        IReadOnlyList<MidiProgram> programs = category != null
            ? GeneralMidi.GetByCategory(category)
            : GeneralMidi.All;

        var result = programs.Select(program => new
        {
            number = program.ProgramNumber,
            name = program.Name,
            category = program.Category,
            bankMsb = program.BankMsb,
            bankLsb = program.BankLsb
        }).ToList();
        return JsonSerializer.Serialize(new { count = result.Count, programs = result });
    }

    private static MidiProgram? ResolveProgram(string program)
    {
        if (byte.TryParse(program, out byte programNumber) && programNumber <= 127)
            return GeneralMidi.GetProgram(programNumber);

        return GeneralMidi.FindByName(program);
    }

    private static void ApplyPatch(ChipCraft.Engine.Sequencer.Song song, int channel, MidiProgram patch)
    {
        song.SetChannelProgram(channel, patch);
        song.ChannelReverbSends[channel] = patch.ReverbSend;
        song.ChannelChorusSends[channel] = patch.ChorusSend;
    }

    private static bool IsValidChannel(int channelCount, int channel) =>
        channel >= 0 && channel < channelCount;
}
