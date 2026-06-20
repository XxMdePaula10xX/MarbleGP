namespace MarbleGP.Core
{
    /// <summary>
    /// Efeitos agregados dos upgrades da equipe (PRD 30), aplicados as bolinhas
    /// do jogador durante a corrida. Valores neutros por padrao (sem efeito).
    /// </summary>
    public struct TeamUpgradeEffects
    {
        public float pitTimeReduction;  // segundos a menos no pit (PitCrew)
        public float energyFactor;      // multiplicador de consumo (EnergyCoreLab)
        public float wearFactor;        // multiplicador de desgaste (GripResearch)
        public float speedFactor;       // multiplicador de velocidade (MarbleMaterial)
        public float controlFactor;     // multiplicador de controle (SurfaceLab)
        public float errorFactor;       // multiplicador de chance de erro (StrategyCenter/AICoaching)

        public static TeamUpgradeEffects Neutral => new TeamUpgradeEffects
        {
            pitTimeReduction = 0f,
            energyFactor = 1f,
            wearFactor = 1f,
            speedFactor = 1f,
            controlFactor = 1f,
            errorFactor = 1f
        };
    }
}
