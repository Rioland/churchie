namespace Churchie.Core.Services;

/// <summary>
/// Where Churchie keeps its data.
///   Windows: %AppData%\Churchie
///   macOS:   ~/Library/Application Support/Churchie
///   Linux:   ~/.config/Churchie
/// Set the CHURCHIE_DATA environment variable to use another folder (e.g. a USB stick).
/// </summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string Songs => Path.Combine(Root, "Songs");
    public string Bibles => Path.Combine(Root, "Bibles");
    public string Schedules => Path.Combine(Root, "Schedules");
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string AutosaveSchedule => Path.Combine(Root, "last-schedule.churchie");

    public AppPaths(string? root = null)
    {
        Root = root
               ?? Environment.GetEnvironmentVariable("CHURCHIE_DATA")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Churchie");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Bibles);
        Directory.CreateDirectory(Schedules);
    }
}
