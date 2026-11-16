using System.Globalization;
using System.IO;
using System.Text;

namespace PauseCue;

/// <summary>One continuous stretch of work, as stored in work-log.csv.</summary>
internal sealed record WorkSession(DateTime Start, DateTime End, string Reason)
{
    public TimeSpan Duration => End - Start;

    /// <summary>The part of this session that falls on <paramref name="day"/>, or null if none.</summary>
    public (DateTime Start, DateTime End)? ClipTo(DateTime day)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        var start = Start > dayStart ? Start : dayStart;
        var end = End < dayEnd ? End : dayEnd;
        return end > start ? (start, end) : null;
    }

    public string ReasonLabel => Reason switch
    {
        "Idle" => "離席",
        "Sleep" => "スリープ",
        "Sleep/long pause" => "スリープ / 長い中断",
        "Break" => "休憩",
        "Exit" => "アプリ終了",
        "Active" => "作業中",
        _ => Reason
    };
}

internal static class WorkLog
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string Header = "Start,End,Minutes,Reason";

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PauseCue", "work-log.csv");

    /// <summary>Raised on the calling thread after a session has been appended.</summary>
    public static event Action? Recorded;

    public static void Record(DateTime start, DateTime end, string reason)
    {
        if (end <= start) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var needsHeader = !File.Exists(LogPath) || new FileInfo(LogPath).Length == 0;
            using var writer = new StreamWriter(LogPath, append: true, new UTF8Encoding(true));
            if (needsHeader) writer.WriteLine(Header);
            writer.WriteLine(string.Join(",",
                start.ToString(TimeFormat, CultureInfo.InvariantCulture),
                end.ToString(TimeFormat, CultureInfo.InvariantCulture),
                (end - start).TotalMinutes.ToString("F1", CultureInfo.InvariantCulture),
                reason));
        }
        catch (IOException)
        {
            // The CSV may be open in another program; losing one entry is better than crashing.
            return;
        }
        Recorded?.Invoke();
    }

    /// <summary>Reads every valid session in the log, oldest first.</summary>
    public static List<WorkSession> ReadAll()
    {
        var sessions = new List<WorkSession>();
        if (!File.Exists(LogPath)) return sessions;
        IEnumerable<string> lines;
        try
        {
            // Allow reading while another program (e.g. Excel) has the file open.
            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (IOException)
        {
            return sessions;
        }

        foreach (var raw in lines.Skip(1))
        {
            var fields = raw.TrimEnd('\r').Split(',');
            if (fields.Length < 2) continue;
            if (!DateTime.TryParseExact(fields[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) continue;
            if (!DateTime.TryParseExact(fields[1], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) continue;
            if (end <= start) continue;
            sessions.Add(new WorkSession(start, end, fields.Length > 3 ? fields[3] : ""));
        }
        sessions.Sort((a, b) => a.Start.CompareTo(b.Start));
        return sessions;
    }

    /// <summary>Minutes worked on <paramref name="day"/>, splitting sessions that cross midnight.</summary>
    public static double MinutesOn(IEnumerable<WorkSession> sessions, DateTime day) =>
        sessions.Sum(s => s.ClipTo(day) is { } part ? (part.End - part.Start).TotalMinutes : 0);

    public static double MinutesToday() => MinutesOn(ReadAll(), DateTime.Today);

    /// <summary>Creates the CSV (header only) if needed so it can be opened externally.</summary>
    public static void EnsureFileExists()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        if (!File.Exists(LogPath)) File.WriteAllText(LogPath, Header + "\r\n", new UTF8Encoding(true));
    }

    public static string PathName => LogPath;
}
