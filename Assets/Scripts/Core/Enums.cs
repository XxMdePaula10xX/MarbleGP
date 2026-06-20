namespace MarbleGP.Core
{
    // ---------------------------------------------------------------------
    // Enums centrais do jogo (PRD secoes 12, 14, 16, 17, 24, 25).
    // Mantidos num unico arquivo para facilitar referencia e expansao.
    // ---------------------------------------------------------------------

    /// <summary>Estilo geral da equipe (PRD 11 / 24.1).</summary>
    public enum TeamStyle
    {
        Aggressive,
        Balanced,
        Technical,
        Strategic,
        Fast,
        Precise,
        Resistant,
        Unpredictable,
        Premium,
        AllOut,
        Fluid
    }

    /// <summary>Bonus passivo leve da equipe (PRD 11).</summary>
    public enum TeamBonusType
    {
        None,
        TopSpeed,
        Consistency,
        Cornering,
        PitStops,
        Acceleration,
        WetWeather,
        LowDamage,
        Overtaking,
        Development,
        EarlyPace
    }

    /// <summary>Personalidade da bolinha; influencia a IA (PRD 12).</summary>
    public enum Personality
    {
        Balanced,
        Aggressive,
        Conservative,
        RiskTaker,
        Defensive,
        Smooth,
        Rookie,
        Veteran
    }

    /// <summary>Tipos de anel de aderencia (PRD 14).</summary>
    public enum GripType
    {
        Soft,
        Medium,
        Hard,
        Intermediate,
        Rain
    }

    /// <summary>Superficie / textura da bolinha (PRD 16).</summary>
    public enum SurfaceType
    {
        Polished,
        MicroGrooved,
        Textured,
        Rough,
        AeroSmooth
    }

    /// <summary>Modo de corrida ajustavel durante a prova (PRD 17).</summary>
    public enum RaceMode
    {
        Normal,
        Push,
        Save,
        Attack,
        Defend,
        Cooldown
    }

    /// <summary>Condicao climatica (PRD 19). MVP usa Dry fixo.</summary>
    public enum Weather
    {
        Dry,
        Cloudy,
        Damp,
        LightRain,
        HeavyRain
    }

    /// <summary>Dificuldade do circuito / da IA (PRD 21 / 7.1).</summary>
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>Estado global da corrida (PRD 25).</summary>
    public enum RaceState
    {
        PreRace,
        Countdown,
        Racing,
        Paused,
        Finished
    }

    /// <summary>Estado individual de cada bolinha (PRD 25).</summary>
    public enum MarbleRaceState
    {
        OnGrid,
        Racing,
        EnteringPit,
        InPit,
        ExitingPit,
        Finished,
        Recovering,
        Retired
    }

    /// <summary>Linha de corrida escolhida pela IA (PRD 13.3).</summary>
    public enum RacingLine
    {
        Ideal,
        Inside,
        Outside,
        Pit
    }
}
