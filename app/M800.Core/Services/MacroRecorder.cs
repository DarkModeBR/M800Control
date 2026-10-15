using M800.Core.Protocol;

namespace M800.Core.Services;

/// <summary>Junta os eventos gravados (com horário) e gera a macro com os atrasos.</summary>
public sealed class MacroRecorder
{
    private readonly List<(MacroEventKind Kind, byte Code, bool Release, long TimeMs)> _events = [];
    private readonly List<(MacroEventKind Kind, byte Code)> _down = [];

    public int Count => _events.Count;

    /// <summary>Sem espaço para mais um "apertar + soltar" (reserva os "soltar" pendentes).</summary>
    public bool IsFull => _events.Count + _down.Count + 2 > Payloads.MaxMacroEvents;

    /// <returns>true se o evento entrou na gravação.</returns>
    public bool Add(MacroEventKind kind, byte code, bool release, long timeMs)
    {
        var key = (kind, code);
        if (release)
        {
            if (!_down.Remove(key))
                return false; // soltou algo que já estava apertado antes de gravar
        }
        else
        {
            if (_down.Contains(key) || IsFull)
                return false; // repetição automática do teclado, ou macro cheia
            _down.Add(key);
        }
        _events.Add((kind, code, release, timeMs));
        return true;
    }

    public List<MacroEvent> Build(bool realDelays, int fixedDelayMs)
    {
        var all = _events.ToList();
        long end = all.Count > 0 ? all[^1].TimeMs : 0;
        foreach (var (kind, code) in Enumerable.Reverse(_down))
            all.Add((kind, code, true, end)); // solta o que ficou apertado

        var result = new List<MacroEvent>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            int delay = realDelays
                ? (i + 1 < all.Count ? (int)Math.Clamp(all[i + 1].TimeMs - all[i].TimeMs, 0, MacroEvent.MaxDelayMs) : 0)
                : Math.Clamp(fixedDelayMs, 0, MacroEvent.MaxDelayMs);
            result.Add(new MacroEvent(all[i].Kind, all[i].Code, all[i].Release, delay));
        }
        return result;
    }
}
