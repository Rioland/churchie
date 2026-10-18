using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>Loads and saves <see cref="AppSettings"/>.</summary>
public static class SettingsStore
{
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonFile.Read<AppSettings>(path) ?? new AppSettings();
        }
        catch
        {
            // A damaged settings file should never stop the app from starting.
        }
        return new AppSettings();
    }

    public static void Save(string path, AppSettings settings) => JsonFile.Write(path, settings);
}

/// <summary>Loads and saves service schedules (.churchie files).</summary>
public static class ScheduleStore
{
    public const string Extension = ".churchie";

    public static Schedule Load(string path) =>
        JsonFile.Read<Schedule>(path) ?? throw new InvalidDataException("The schedule file is empty");

    public static void Save(string path, Schedule schedule) => JsonFile.Write(path, schedule);
}
