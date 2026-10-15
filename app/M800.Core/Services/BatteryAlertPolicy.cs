using M800.Core.Protocol;

namespace M800.Core.Services;

/// <summary>Avisa uma vez por descarga quando a bateria fica ≤ limite; rearma ao carregar ou ao passar de limite+5.</summary>
public sealed class BatteryAlertPolicy
{
    private bool _armed = true;

    public bool ShouldAlert(DeviceInfo info, bool enabled, int threshold)
    {
        if (info.Charge != ChargeState.Discharging || info.Battery > threshold + 5)
        {
            _armed = true;
            return false;
        }
        if (!enabled || !_armed || info.Battery > threshold)
            return false;
        _armed = false;
        return true;
    }
}
