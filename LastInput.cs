using System.Runtime.InteropServices;

namespace PauseCue;

internal static class LastInput
{
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Size; public uint Time; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref Info info);
    public static TimeSpan Since()
    {
        var info = new Info { Size = (uint)Marshal.SizeOf<Info>() };
        GetLastInputInfo(ref info);
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }
}
