namespace MarbleGP.Save
{
    /// <summary>
    /// Progresso do Desafio Diário (PRD extra). Guarda o melhor resultado do dia
    /// e a sequência (streak) de dias cumprindo o objetivo. Persistido em
    /// daily.json. O desafio em si é derivado da data (mesmo para todo mundo).
    /// </summary>
    [System.Serializable]
    public class DailyData
    {
        public string lastDateKey = "";   // "yyyyMMdd" do desafio atualmente refletido
        public bool objectiveMet;          // o objetivo de hoje ja foi cumprido?
        public int bestPosition;           // melhor posicao do jogador hoje (0 = nao correu)
        public int bestOvertakes;          // mais ultrapassagens numa tentativa de hoje
        public int attempts;               // tentativas de hoje
        public int streak;                 // dias seguidos cumprindo o objetivo
        public string lastWinDateKey = ""; // ultimo dia em que cumpriu (para a streak)
    }
}
