using System.IO;

namespace PauseCue;

public static class StartupRegistrar
{
    private const string LinkName = "Pause Cue.lnk";
    private static string LinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), LinkName);

    public static bool IsEnabled => File.Exists(LinkPath);

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(LinkPath)) File.Delete(LinkPath);
            return;
        }

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルの場所を取得できません。");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host を利用できません。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(LinkPath);
        shortcut.TargetPath = executable;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executable)!;
        shortcut.Description = "Pause Cue を起動";
        shortcut.Save();
    }
}
