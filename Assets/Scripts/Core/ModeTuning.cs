namespace MarbleGP.Core
{
    /// <summary>Multiplicadores de um modo de corrida (PRD 4.2 / 5.2).</summary>
    public struct ModeTune
    {
        public float speed;        // multiplicador de velocidade
        public float wear;         // multiplicador de desgaste
        public float fuel;         // multiplicador de consumo de combustivel
        public float energyDelta;  // variacao de energia por volta (+regen / -dreno)
        public float errorMult;    // multiplicador da chance de erro
    }

    /// <summary>
    /// Tabela de balanceamento dos modos (PRD 4.2). Em codigo (nao serializado)
    /// para nao depender de assets antigos do GameBalance.
    /// </summary>
    public static class ModeTuning
    {
        public static ModeTune Get(RaceMode mode)
        {
            switch (mode)
            {
                case RaceMode.Push:
                    return new ModeTune { speed = 1.08f, wear = 1.25f, fuel = 1.35f, energyDelta = -22f, errorMult = 1.20f };
                case RaceMode.Save:
                    return new ModeTune { speed = 0.92f, wear = 0.75f, fuel = 0.78f, energyDelta = 8f, errorMult = 0.80f };
                case RaceMode.Attack:
                    return new ModeTune { speed = 1.10f, wear = 1.30f, fuel = 1.40f, energyDelta = -24f, errorMult = 1.25f };
                case RaceMode.Defend:
                    return new ModeTune { speed = 0.96f, wear = 0.95f, fuel = 0.95f, energyDelta = -6f, errorMult = 0.95f };
                case RaceMode.Cooldown:
                    return new ModeTune { speed = 0.90f, wear = 0.70f, fuel = 0.80f, energyDelta = 6f, errorMult = 0.80f };
                default: // Normal
                    return new ModeTune { speed = 1.00f, wear = 1.00f, fuel = 1.00f, energyDelta = -8f, errorMult = 1.00f };
            }
        }
    }
}
