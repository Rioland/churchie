using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>
/// The song database. Each song is one JSON file in the Songs folder, which keeps
/// things simple, easy to back up, and safe (one bad file can't break the rest).
/// </summary>
public sealed class SongLibrary
{
    private readonly string _folder;
    private readonly List<Song> _songs = new();

    public IReadOnlyList<Song> Songs => _songs;
    public List<string> LoadErrors { get; } = new();

    public SongLibrary(string folder)
    {
        _folder = folder;
    }

    /// <summary>Loads all songs. On the very first run, adds a few public-domain hymns.</summary>
    public void Load()
    {
        _songs.Clear();
        var firstRun = !Directory.Exists(_folder);
        Directory.CreateDirectory(_folder);

        if (firstRun)
        {
            foreach (var song in SampleSongs.All()) Save(song);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                var song = JsonFile.Read<Song>(file);
                if (song is not null) _songs.Add(song);
            }
            catch (Exception ex)
            {
                LoadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        _songs.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
    }

    public Song? Find(Guid id) => _songs.FirstOrDefault(s => s.Id == id);

    /// <summary>Adds a new song or updates an existing one, and writes it to disk.</summary>
    public void Save(Song song)
    {
        song.UpdatedAt = DateTime.UtcNow;
        JsonFile.Write(PathFor(song.Id), song);

        var index = _songs.FindIndex(s => s.Id == song.Id);
        if (index >= 0) _songs[index] = song;
        else _songs.Add(song);
        _songs.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
    }

    public void Delete(Guid id)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
        _songs.RemoveAll(s => s.Id == id);
    }

    /// <summary>Songs whose title matches come first, then songs with matching lyrics.</summary>
    public IEnumerable<Song> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _songs;
        var q = query.Trim();

        var byTitle = _songs.Where(s => s.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        var byWords = _songs.Where(s =>
            !s.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) &&
            (s.AllText.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
             s.Author.Contains(q, StringComparison.CurrentCultureIgnoreCase)));
        return byTitle.Concat(byWords);
    }

    private string PathFor(Guid id) => Path.Combine(_folder, $"{id}.json");
}
