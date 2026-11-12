using System.Globalization;
using System.IO;
using System.Text;

namespace PauseCue;

internal static class WorkLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PauseCue", "work-log.csv");

    public static void Record(DateTime start, DateTime end, string reason)
    {
        if (end <= start) return;
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        var needsHeader = !File.Exists(LogPath) || new FileInfo(LogPath).Length == 0;
        using var writer = new StreamWriter(LogPath, append: true, new UTF8Encoding(true));
        if (needsHeader) writer.WriteLine("Start,End,Minutes,Reason");
        writer.WriteLine(string.Join(",",
            start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            (end - start).TotalMinutes.ToString("F1", CultureInfo.InvariantCulture),
            reason));
    }

    public static double MinutesToday()
    {
        if (!File.Exists(LogPath)) return 0;
        var today = DateTime.Today;
        double total = 0;
        foreach (var line in File.ReadLines(LogPath).Skip(1))
        {
            var fields = line.Split(',');
            if (fields.Length < 3 || !DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) continue;
            if (start.Date == today && double.TryParse(fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes)) total += minutes;
        }
        return total;
    }

    public static string PathName => LogPath;
}
