namespace MarbleGP.Core
{
    /// <summary>
    /// Condicoes vivas da corrida consultadas pela IA e pelas formulas
    /// (PRD 19 clima dinamico / PRD 20 eventos). Implementado pelo RaceManager.
    /// </summary>
    public interface IRaceConditions
    {
        /// <summary>Clima atual (pode mudar durante a corrida).</summary>
        Weather CurrentWeather { get; }

        /// <summary>Multiplicador global de velocidade por eventos (ex.: pista suja).</summary>
        float TrackSpeedMod { get; }

        /// <summary>Acrescimo a chance de erro por eventos (0..1).</summary>
        float TrackErrorAdd { get; }

        /// <summary>Safety Marble em pista: velocidade neutralizada (PRD 20).</summary>
        bool SafetyMarbleActive { get; }
    }
}
