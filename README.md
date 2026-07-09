# Marble GP Manager

Jogo de gerência de corridas de **bolinhas de gude autônomas**, vistas de cima
(top-down). O jogador é o **chefe de equipe/estrategista**: não pilota as bolinhas,
mas decide pit stops, anéis de aderência, energia, superfície e modo de corrida.

> Inspirado no conceito geral de corridas de elite, **sem** usar nomes, logos,
> marcas, pistas ou equipes oficiais de qualquer categoria real.

## ⚡ Versão atual: Web (TypeScript + Canvas 2D + Capacitor)

O jogo foi **migrado do Unity para a web** (pasta [`web/`](web/)):
**TypeScript estrito + Canvas 2D + Vite**, empacotado para **iOS via Capacitor**.
O port é 1:1 — mesmos dados, fórmulas do PRD 41, balanceamento e telas.

Vantagens da nova stack:
- Bundle de **~40 KB gzip** (vs. dezenas de MB do Unity).
- Roda no navegador, como PWA e como app nativo iOS (mesma base de código).
- Testável de ponta a ponta sem editor (typecheck + simulação headless +
  screenshots reais via Chromium).

### Rodar localmente

```bash
cd web
npm install
npm run dev        # http://localhost:5173
```

- `npm run typecheck` — verificação de tipos (tsc estrito).
- `npm run build` — build de produção em `web/dist/`.
- Simulação headless de uma corrida completa (sem navegador):
  `npx esbuild --bundle src/dev/simtest.ts --format=esm --outfile=/tmp/simtest.mjs && node /tmp/simtest.mjs`

### Estrutura (`web/src/`)

| Pasta | Responsabilidade |
|-------|------------------|
| `core/` | Tipos e helpers (enums, multiplicadores de atributo, clima × pneu) |
| `data/` | Dados do jogo: 10 equipes, 20 bolinhas, 15 circuitos, 5 compostos, 3 superfícies, balanceamento |
| `sim/` | Simulação: fórmulas (PRD 41), pista (Catmull-Rom + checkpoints + pit lane), steering, posições, pit stops, estratégia da IA, clima dinâmico, Safety Marble, rádio do box, gravador de replay |
| `game/` | Meta-game: save (localStorage), campeonato + upgrades, 32 conquistas, desafio diário, notificações locais (Capacitor) |
| `render/` | Canvas 2D: ambiente pré-renderizado offscreen, pista vetorial, bolinhas, câmera zoom/pan/follow, miniaturas procedurais |
| `ui/` | 11 telas DOM (perfil, menu, corrida com HUD completo, resultado, campeonato, garagem, conquistas, daily, replay…) |

### Deploy iOS (TestFlight, sem Mac)

O `codemagic.yaml` na raiz tem o workflow **`capacitor-ios`**:
`npm ci` → `npm run build` → `cap sync ios` → IPA assinado → TestFlight.

Pré-requisitos no Codemagic:
1. Integração App Store Connect (chave API `.p8`) chamada **`MarbleGP_ASC`**.
2. Bundle ID **`com.matheuscastro.marblegp`** registrado no App Store Connect.

O projeto Xcode (`web/ios/`) já está no repositório com paisagem travada,
ícone e splash oficiais.

### Fluxo do jogo

Criar Perfil → Menu → { Corrida Rápida · Desafio do Dia · Campeonato ·
Garagem · Conquistas } → Seleção de Circuito → Estratégia (pneu/modo/duração) →
**Corrida** (torre de tempos, cards com PIT/MODO/PNEU, rádio do box, eventos,
Safety Marble, clima dinâmico, zoom/pan/follow) → Resultado → Replay.

---

## Conteúdo do jogo

- **10 equipes** × 2 bolinhas = grid de 20, cada piloto com 10 atributos e
  personalidade própria (afeta ritmo, ultrapassagem, erros e estratégia).
- **15 circuitos** com geometria procedural própria (spline fechada por
  control points; 12 gerados pela curva paramétrica `r = 1 + amp·sin(lobes·θ)`).
- **5 compostos** (Soft/Medium/Hard/Inter/Rain) com desgaste, consumo e
  performance por clima; **3 superfícies** de bolinha.
- **Clima dinâmico** por volta (Seco↔Nublado↔Úmido↔Chuva leve↔Chuva forte).
- **Campeonato** com classificação de pilotos/equipes, créditos e **7 upgrades**
  de 5 níveis; **32 conquistas**; **Desafio do Dia** com seed por data e streak;
  **replay** com destaques; **notificações locais** com badge.

---

## Versão Unity (legado)

O projeto Unity original (C#, `Assets/`) permanece no repositório como
referência durante a transição. Para rodá-lo: Unity 2022.3 LTS →
`Tools > Marble GP > Setup Completo (Dados + Cena)` → Play na cena
`Assets/Scenes/Bootstrap.unity`. O workflow Codemagic `unity-ios` (legado)
ainda existe no `codemagic.yaml`.
