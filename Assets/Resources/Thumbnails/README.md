# Miniaturas dos circuitos (Thumbnails)

Arte/mini-mapa top-down de cada circuito, exibida nos cards da tela
"Escolha o Circuito". **Já está ligada no jogo** (`UIFactory.Thumbnail`).
O nome do arquivo deve bater **exatamente** com o `trackId`. Se faltar, o card
mostra uma moldura escura no lugar (nada quebra).

## Tamanho exato

**512×512 px (quadrada, 1:1)**, PNG ou JPG.

## Nomes EXATOS dos arquivos (= trackId)

Circuitos jogáveis (MVP):

| Arquivo               | Circuito          |
|-----------------------|-------------------|
| `marble_park.png`     | Marble Park       |
| `neon_harbor.png`     | Neon Harbor       |
| `spiral_canyon.png`   | Spiral Canyon     |

Circuitos do campeonato (placeholders, opcionais):

`sakura_speedway`, `desert_loop`, `ice_bowl`, `volcano_ring`, `rainforest_gp`,
`metro_marble`, `royal_garden`, `skybridge`, `factory_run`, `moonbase_gp`,
`atlantis_drift`, `final_orbit`

> Placeholder para o git versionar a pasta.
