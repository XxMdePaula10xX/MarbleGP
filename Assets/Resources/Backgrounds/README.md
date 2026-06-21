# Fundos de tela (Backgrounds)

Coloque aqui as imagens de fundo das telas. O jogo carrega automaticamente
(`UIFactory.Background`) e aplica um leve escurecimento por cima para legibilidade.
Se um arquivo não existir, a tela usa um fundo escuro padrão.

## Nomes EXATOS dos arquivos (.png ou .jpg)

| Arquivo            | Tela                                   |
|--------------------|----------------------------------------|
| `menu.png`         | Menu principal + criação de perfil     |
| `garage.png`       | Garagem                                |
| `strategy.png`     | Estratégia pré-corrida                 |
| `trackselect.png`  | Seleção de circuito                    |
| `championship.png` | Campeonato / Upgrades                  |
| `results.png`      | Resultado da corrida                   |

- Resolução recomendada: **1920×1080**.
- Pode usar imagens vibrantes (há overlay escuro de ~45% por cima).
- No Unity, a textura pode ficar com import "Default" mesmo (o código usa RawImage/Texture2D).

> Este README é só um placeholder para o git versionar a pasta. Pode mantê-lo ou apagar.
