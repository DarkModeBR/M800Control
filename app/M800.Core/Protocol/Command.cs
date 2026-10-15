namespace M800.Core.Protocol;

public enum Command : byte
{
    Key = 0x04,
    Dpi = 0x05,
    DpiColors = 0x06,
    PollingRate = 0x07,
    Lighting = 0x08,
    LiftOff = 0x09,
    Debounce = 0x0A,
    Power = 0x0B,
    MotionWakeup = 0x0C,
    Macro = 0x0E,
    FactoryReset = 0x0F,
    Info = 0x20,
}
