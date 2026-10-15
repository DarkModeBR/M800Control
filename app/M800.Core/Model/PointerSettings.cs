namespace M800.Core.Model;

/// <summary>Configurações do ponteiro do Windows guardadas no perfil (aplicadas só se Enabled).</summary>
public sealed class PointerSettings
{
    /// <summary>Se falso, o app não mexe nas configurações do Windows.</summary>
    public bool Enabled { get; set; }

    /// <summary>Velocidade do ponteiro do Windows, 1 a 20 (10 = 1:1).</summary>
    public int Speed { get; set; } = 10;

    /// <summary>"Aumentar precisão do ponteiro" (aceleração).</summary>
    public bool Acceleration { get; set; } = true;

    /// <summary>Linhas por clique da roda, 1 a 100; 0 = uma tela por vez.</summary>
    public int ScrollLines { get; set; } = 3;

    /// <summary>Tempo máximo entre os cliques de um clique duplo, 200 a 900 ms.</summary>
    public int DoubleClickMs { get; set; } = 500;

    /// <summary>Recomendado para jogos: movimento 1:1, sem aceleração.</summary>
    public void ApplyGameMode()
    {
        Speed = 10;
        Acceleration = false;
    }

    public void Normalize()
    {
        Speed = Math.Clamp(Speed, 1, 20);
        ScrollLines = Math.Clamp(ScrollLines, 0, 100);
        DoubleClickMs = Math.Clamp(DoubleClickMs, 200, 900);
    }
}
