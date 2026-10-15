using System.Numerics;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.Core.Services;

public sealed record OutgoingPacket(Command Command, byte[] Payload, byte Chunk = 0x01);

/// <summary>Converte uma seção do perfil nos pacotes que o mouse espera.</summary>
public static class ProfileEncoder
{
    public static IReadOnlyList<OutgoingPacket> Encode(Profile p, Section section, SensorDpiTable table) => section switch
    {
        Section.Dpi => [EncodeDpi(p, table)],
        Section.DpiColors => [new(Command.DpiColors, Payloads.DpiColors(DpiColors(p)))],
        Section.Polling => [new(Command.PollingRate, Payloads.PollingRate(p.PollingRateHz))],
        Section.Lighting => [new(Command.Lighting, Payloads.Lighting(p.Lighting.Mode, p.Lighting.Brightness,
            p.Lighting.Speed, p.Lighting.ColorsFor(p.Lighting.Mode), p.Lighting.Direction))],
        Section.LiftOff => [new(Command.LiftOff, Payloads.LiftOff(p.LiftOffMm))],
        Section.Debounce => [new(Command.Debounce, Payloads.Debounce(p.DebounceMs))],
        Section.Power => [new(Command.Power, Payloads.Power(p.SleepMinutes, p.LightOffWhileMoving))],
        Section.MotionWakeup => [new(Command.MotionWakeup, Payloads.MotionWakeup(p.MotionWakeup))],
        Section.Buttons => EncodeButtons(p),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    public static IReadOnlyList<OutgoingPacket> EncodeAll(Profile p, SensorDpiTable table) =>
        Sections.All.SelectMany(s => Encode(p, s, table)).ToList();

    public static OutgoingPacket FactoryReset() => new(Command.FactoryReset, Payloads.FactoryReset());

    public static byte EnabledMask(Profile p)
    {
        byte mask = 0;
        for (int i = 0; i < p.DpiLevels.Count && i < ProtocolConstants.MaxDpiLevels; i++)
        {
            if (p.DpiLevels[i].Enabled)
                mask |= (byte)(1 << i);
        }
        return mask == 0 ? (byte)1 : mask;
    }

    /// <summary>Nível ativo válido: o escolhido, se habilitado; senão o primeiro habilitado.</summary>
    public static int ActiveLevel(Profile p)
    {
        byte mask = EnabledMask(p);
        int active = p.ActiveDpiLevel;
        return active is >= 0 and < 8 && (mask & (1 << active)) != 0 ? active : BitOperations.TrailingZeroCount(mask);
    }

    private static OutgoingPacket EncodeDpi(Profile p, SensorDpiTable table)
    {
        var registers = p.DpiLevels.Take(ProtocolConstants.MaxDpiLevels)
            .Select(l => table.ToRegister(table.Nearest(l.Dpi)))
            .ToList();
        return new(Command.Dpi, Payloads.Dpi(registers, ActiveLevel(p), EnabledMask(p)));
    }

    private static List<Rgb> DpiColors(Profile p)
    {
        var colors = p.DpiLevels.Select(l => l.Color).Take(8).ToList();
        while (colors.Count < 8)
            colors.Add(Rgb.White);
        return colors;
    }

    private static List<OutgoingPacket> EncodeButtons(Profile p)
    {
        var packets = new List<OutgoingPacket>();
        var slots = new Dictionary<Guid, int>();
        var actions = new List<ButtonAction>();

        foreach (var id in ButtonIds.All)
        {
            int i = (int)id;
            var binding = i < p.Buttons.Count ? p.Buttons[i] : ButtonBinding.For(ButtonAction.DefaultFor(id));
            if (binding.MacroId is not Guid macroId)
            {
                actions.Add(binding.Action);
                continue;
            }
            var macro = p.Macros.FirstOrDefault(m => m.Id == macroId);
            if (macro is null)
            {
                actions.Add(ButtonAction.DefaultFor(id));
                continue;
            }
            if (!slots.TryGetValue(macroId, out int slot))
            {
                slot = slots.Count + 1;
                slots[macroId] = slot;
                foreach (var (chunk, payload) in Payloads.MacroChunks(slot, macro.Events))
                    packets.Add(new(Command.Macro, payload, chunk));
            }
            actions.Add(ButtonAction.Macro(slot, Math.Clamp(binding.MacroRepeat, 1, 255)));
        }

        foreach (var id in ButtonIds.All)
            packets.Add(new(Command.Key, Payloads.Key(id, actions[(int)id])));
        return packets;
    }
}
