using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.Core.Tests;

public class BatteryAlertPolicyTests
{
    private static DeviceInfo Info(int battery, ChargeState charge = ChargeState.Discharging) =>
        new("M802", 0x70, battery, charge, true);

    [Fact]
    public void Alerts_once_per_discharge()
    {
        var policy = new BatteryAlertPolicy();
        Assert.False(policy.ShouldAlert(Info(40), true, 15));
        Assert.True(policy.ShouldAlert(Info(15), true, 15));
        Assert.False(policy.ShouldAlert(Info(12), true, 15));
        Assert.False(policy.ShouldAlert(Info(5), true, 15));
    }

    [Fact]
    public void Rearms_after_charging()
    {
        var policy = new BatteryAlertPolicy();
        Assert.True(policy.ShouldAlert(Info(10), true, 15));
        Assert.False(policy.ShouldAlert(Info(12, ChargeState.Charging), true, 15));
        Assert.True(policy.ShouldAlert(Info(11), true, 15));
    }

    [Fact]
    public void Rearms_when_battery_rises_above_threshold_plus_5()
    {
        var policy = new BatteryAlertPolicy();
        Assert.True(policy.ShouldAlert(Info(14), true, 15));
        Assert.False(policy.ShouldAlert(Info(18), true, 15));
        Assert.False(policy.ShouldAlert(Info(15), true, 15));
        Assert.False(policy.ShouldAlert(Info(21), true, 15));
        Assert.True(policy.ShouldAlert(Info(15), true, 15));
    }

    [Fact]
    public void Disabled_never_alerts() => Assert.False(new BatteryAlertPolicy().ShouldAlert(Info(1), false, 15));
}
