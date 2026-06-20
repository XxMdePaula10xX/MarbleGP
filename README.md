# Marble GP Manager

Jogo de gerência de corridas de **bolinhas de gude 3D autônomas**, vistas de cima
(top-down). O jogador é o **chefe de equipe/estrategista**: não pilota as bolinhas,
mas decide pit stops, anéis de aderência, energia, superfície e modo de corrida.

> Inspirado no conceito geral de corridas de elite, **sem** usar nomes, logos,
> marcas, pistas ou equipes oficiais de qualquer categoria real.

Engine: **Unity 2022.3 LTS** · Linguagem: **C#**

---

## Como rodar (passo a passo)

Este projeto foi construído numa abordagem **code-first / procedural**: pista,
bolinhas e HUD são gerados por código, e os dados (equipes, bolinhas, pistas,
anéis, superfícies) são gerados por um menu de Editor. Isso permite abrir e
jogar com setup mínimo, sem montar cenas/prefabs à mão.

1. **Instale o Unity 2022.3 LTS** (Unity Hub → adicionar versão `2022.3.x`).
2. **Abra o projeto** (pasta raiz deste repositório) pelo Unity Hub.
   - Na primeira importação a Unity gera os arquivos `.meta` automaticamente.
3. No menu superior, rode: **`Tools > Marble GP > Setup Completo (Dados + Cena)`**
   - Isso gera todos os ScriptableObjects do MVP em `Assets/Resources/GameDatabase.asset`
     e cria a cena `Assets/Scenes/Bootstrap.unity` (já adicionada ao Build Settings).
4. Abra a cena `Assets/Scenes/Bootstrap.unity` e dê **Play**.

> Alternativamente, rode os dois menus separadamente:
> `Tools > Marble GP > Gerar Dados do MVP` e `Tools > Marble GP > Criar Cena Bootstrap`.

### Fluxo no jogo
Criar Perfil → Menu Principal → Corrida Rápida → Selecionar Circuito →
Estratégia (anel + modo) → **Corrida** (HUD com ranking, pit e modo) → Resultado.

Durante a corrida:
- **Botão PIT** (painel da direita) chama o pit stop da sua bolinha.
- **Botão MODE** cicla Normal → Push → Save.
- **Botão Camera** (canto sup. direito) alterna visão geral / seguir bolinha.
- **Scroll do mouse** dá zoom.

---

## Arquitetura

Código em `Assets/Scripts/`, organizado por domínio e seguindo o PRD:

| Pasta | Responsabilidade |
|-------|------------------|
| `Core/` | `GameManager`, `GameBalance` (PRD 28/41), enums, `RaceConfig`, `QuickRaceBuilder` |
| `Data/` | ScriptableObjects: `TeamDataSO`, `MarbleDriverSO`, `TrackDataSO`, `GripRingSO`, `SurfaceProfileSO`, `GameDatabase` |
| `Save/` | `PlayerProfile`, `SaveManager` (JSON local) |
| `Track/` | `TrackManager`, `TrackBuilder` (geração procedural + malha), `Lane`, `MaterialFactory` |
| `AI/` | `MarbleController` (física guiada), `MarbleAI` (comportamentos) |
| `Systems/` | `MarbleRuntime`, `RaceFormulas`, `TireWearSystem`, `EnergySystem` |
| `Race/` | `RaceManager`, `RacePositionSystem`, `PitStopManager`, `MarbleFactory`, `RaceResult` |
| `Camera/` | `CameraController` (ortográfica top-down) |
| `UI/` | `UIFactory`, `RaceHUD` (HUD procedural) |
| `Bootstrap/` | `AppController` (orquestra todo o fluxo numa cena única) |
| `Editor/` | `DataGenerator`, `SceneSetup` (menus Tools > Marble GP) |

### Princípios seguidos (PRD 39)
- Sistemas funcionais antes de polimento; lógica de corrida separada da UI.
- Dados em ScriptableObjects, **nada hardcodado** nos managers (via `GameDatabase`).
- Arquitetura pronta para **24 bolinhas** (MVP roda com 8).
- Separação entre **dados base** (`*SO`) e **estado de runtime** (`MarbleRuntime`).
- Sistema anti-bug de bolinha presa (`MarbleController.HandleStuckRecovery`).
- Posição por **progresso de arco** (PRD 26), evitando o bug de circuito fechado.

---

## O que está implementado (MVP — Fases 1–5 do PRD)

✅ Perfil local (JSON) · Menu principal · Corrida Rápida · Seleção de circuito ·
Estratégia pré-corrida · Largada com contagem 3-2-1-GO · IA seguindo waypoints ·
3 linhas de corrida + pit lane · Ranking ao vivo · Contagem de voltas por
checkpoints · Anéis Soft/Medium/Hard · Desgaste · Energia · Modos Normal/Push/Save ·
Pit stop (troca de anel + recarga) · Fim de corrida · Tela de resultado com pontos ·
Câmera top-down (overview/seguir) · 3 pistas jogáveis + 12 placeholders.

✅ **Ranking ao vivo (timing tower)** no estilo da referência: posição · chip/cor
da equipe (logo placeholder) com número · sigla de 3 letras do piloto · gap para
o líder em segundos · indicador do anel (cor estilo pneu) · setas verde/vermelho
de variação de posição · linha do jogador destacada.

✅ **Modo Campeonato** (PRD 29): calendário das etapas, pontuação por posição,
classificação de pilotos e de equipes, vitórias/pódios, histórico, progressão
entre etapas e persistência em JSON. Hub com "Correr Etapa" e standings ao vivo.

✅ **Garagem** (PRD 31): exibe as 2 bolinhas do jogador com atributos e
personalidade, permite editar o nome da equipe, as cores primária/secundária e a
cor de cada bolinha (paleta). As customizações são salvas no perfil e aplicadas
em runtime (corpo da bolinha, faixa, chip do ranking e nome no resultado) sem
mutar os ScriptableObjects base.

✅ **Upgrades de Equipe** (PRD 30): moeda de créditos ganha por corrida no
campeonato e 7 upgrades melhoráveis (Pit Crew, Energy Core Lab, Grip Research,
Surface Lab, Strategy Center, Marble Material, AI Coaching), cada um com 5
níveis. Os efeitos (tempo de pit, consumo, desgaste, velocidade, controle e
chance de erro) são aplicados às bolinhas do jogador em corrida via as fórmulas
do PRD 41. Tela de Upgrades no hub do campeonato, com custos e saldo, persistida
em JSON junto da temporada.

✅ **Clima dinâmico** (PRD 19): o tempo evolui durante a corrida
(Seco↔Nublado↔Úmido↔Chuva leve↔Chuva forte) guiado pela chance de chuva da
pista, afetando ao vivo a performance dos anéis, o desgaste e a chance de erro
(com `WetSkill` ajudando no molhado e penalidade por usar pneu seco na chuva).
Anéis **Intermediate** e **Rain** disponíveis; previsão exibida na estratégia e
clima ao vivo no HUD. Botão **TYRE** no painel permite trocar o composto no pit.

✅ **Eventos de corrida** (PRD 20): **Safety Marble** (neutraliza velocidade e
ultrapassagens por alguns segundos) e **Pista Suja** (menos aderência e mais
risco de erro temporariamente), sorteados ao longo da prova, além dos alertas de
desgaste/energia/pit e dos avisos de mudança de clima — tudo no log do HUD.

### Dados gerados (PRD 42)
- **4 equipes**: Red Comet Racing, Blue Orbit GP, Emerald Rollers, Shadow Marble Team.
- **8 bolinhas** (2 por equipe), com atributos e personalidades distintas.
- **3 pistas jogáveis**: Marble Park, Neon Harbor, Spiral Canyon (+ 12 bloqueadas).
- **3 anéis** (Soft/Medium/Hard) e **3 superfícies** (Polished/Micro-Grooved/Textured).

---

## Próximas fases (Fase 6–7 do PRD) — ainda não implementadas
Expansão do campeonato para 12 equipes / 24 bolinhas / 15 pistas finalizadas,
áudio e efeitos visuais (PRD 32 — Push glow, trail, alertas piscando), eventos
extras (pit lento, undercut de rival), e build mobile (Android/iOS). A
arquitetura já está preparada para esses pontos (o campeonato MVP roda com as
4 equipes / 8 bolinhas / 3 pistas já existentes).

## Limitações conhecidas (MVP)
- Visual com primitivas/placeholder (sem assets externos), conforme PRD 39.8/9.
- A contagem de voltas é validada por checkpoints; em casos de pit muito longo a
  re-sincronização de checkpoint pós-pit é simplificada (a bolinha sempre termina,
  mas o ponto exato de revalidação pode variar). Será refinado na Fase 6.
- Estratégia pré-corrida aplica a mesma configuração inicial às bolinhas do
  jogador; ajuste individual por bolinha entra junto da Garagem (Fase 6).
</content>
