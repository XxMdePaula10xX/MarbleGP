# Ícone do App

Coloque aqui o ícone do aplicativo com o nome **`appicon.png`**:

```
Assets/AppIcon/appicon.png
```

## Requisitos (App Store / Google Play)
- **1024 × 1024 px**, quadrado.
- **PNG opaco — SEM canal alpha / sem transparência** (a arte azul atual já é opaca, ok).
- **Sem cantos arredondados** (a Apple arredonda automaticamente).
- Evite texto pequeno; precisa ser legível em tamanho reduzido.

## Como aplicar
Depois de colocar o `appicon.png` aqui, no Unity rode:

**Tools → Marble GP → Definir Ícone do App**

Isso define o "Default Icon" do projeto; o Unity gera automaticamente todos os
tamanhos de iOS e Android a partir dele. (O menu **Configurar iOS** também
aplica o ícone, se o arquivo já estiver presente.)
