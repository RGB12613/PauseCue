using System.Text.Json;
using System.IO;

namespace PauseCue;

public sealed class UserSettings
{
    public int WorkIntervalMinutes { get; set; } = 45;
    public int NightStartHour { get; set; } = 0;
    public int IdleResetMinutes { get; set; } = 8;
    public DateTime? PausedUntil { get; set; }
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PauseCue", "settings.json");
    public static UserSettings Load()
    {
        try { return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
