namespace M800.Core.Model;

public enum AppTheme { System, Light, Dark }

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool BatteryAlertEnabled { get; set; } = true;
    public int BatteryAlertThreshold { get; set; } = 15;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool FirstRunDone { get; set; }
    public bool DpiIndicatorEnabled { get; set; } = true;
}
