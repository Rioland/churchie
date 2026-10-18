using System.Text.Json;
using System.Text.Json.Serialization;

namespace Churchie.Core.Services;

/// <summary>Small helper for reading and safely writing JSON files.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options);
    }

    /// <summary>
    /// Writes to a temporary file first and then swaps it in, so a crash or power
    /// cut in the middle of saving never leaves a half-written file behind.
    /// </summary>
    public static void Write<T>(string path, T value)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var temp = path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, value, Options);
        File.Move(temp, path, overwrite: true);
    }
}
