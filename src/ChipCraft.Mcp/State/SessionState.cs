using System.Collections.Concurrent;
using ChipCraft.Engine.Composition;
using ChipCraft.Engine.Sequencer;

namespace ChipCraft.Mcp.State;

/// <summary>
/// Stateful session store for the MCP server. Holds patterns and songs
/// created during the session. Registered as a DI singleton.
/// </summary>
public class SessionState
{
    private readonly ConcurrentDictionary<string, Pattern> _patterns = new();
    private readonly ConcurrentDictionary<string, Song> _songs = new();
    private readonly ConcurrentDictionary<string, SongProjectMetadata> _songMetadata = new();

    private int _nextPatternId;
    private int _nextSongId;

    private string? _lastSongId;

    public string AddPattern(Pattern pat)
    {
        if (string.IsNullOrEmpty(pat.Id) || pat.Id.Length < 4)
            pat.Id = $"pat_{Interlocked.Increment(ref _nextPatternId):D3}";
        _patterns[pat.Id] = pat;
        return pat.Id;
    }

    public string AddSong(Song song)
    {
        if (string.IsNullOrEmpty(song.Id) || song.Id.Length < 4)
            song.Id = $"song_{Interlocked.Increment(ref _nextSongId):D3}";

        foreach (var pattern in song.Patterns)
            AddPattern(pattern);

        _songs[song.Id] = song;
        _lastSongId = song.Id;
        return song.Id;
    }

    public Pattern? GetPattern(string id) => _patterns.GetValueOrDefault(id);

    public IReadOnlyList<Pattern> ListPatterns() => _patterns.Values.ToList();

    public Song? GetSong(string? id = null)
    {
        if (id != null) return _songs.GetValueOrDefault(id);
        if (_lastSongId != null) return _songs.GetValueOrDefault(_lastSongId);
        return _songs.Values.FirstOrDefault();
    }

    public Song? GetMostRecentSong() => GetSong();

    public IReadOnlyList<Song> ListSongs() => _songs.Values.ToList();

    public bool TryGetPart(string partId, out Pattern? pattern, out Part? part)
    {
        foreach (var candidatePattern in _patterns.Values)
        {
            var candidatePart = candidatePattern.GetPart(partId);
            if (candidatePart != null)
            {
                pattern = candidatePattern;
                part = candidatePart;
                return true;
            }
        }

        pattern = null;
        part = null;
        return false;
    }

    public void SetSongMetadata(string songId, SongProjectMetadata metadata)
    {
        _songMetadata[songId] = metadata;
    }

    public SongProjectMetadata? GetSongMetadata(string? songId = null)
    {
        var song = GetSong(songId);
        if (song == null)
            return null;

        return _songMetadata.GetValueOrDefault(song.Id);
    }
}
