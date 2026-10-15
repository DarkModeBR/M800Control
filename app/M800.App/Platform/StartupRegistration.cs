using Microsoft.Win32;

namespace M800.App.Platform;

/// <summary>Liga/desliga o início com o Windows (HKCU\...\Run), já minimizado na bandeja.</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "M800Control";

    public static void Sync(bool enabled)
    {
        var exe = Environment.ProcessPath;
        // não registra builds de desenvolvimento (bin\Debug, bin\Release)
        if (exe is null || exe.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase))
            return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
