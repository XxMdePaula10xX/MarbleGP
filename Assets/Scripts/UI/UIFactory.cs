using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarbleGP.UI
{
    /// <summary>
    /// Utilitarios para montar UI (uGUI) por codigo, evitando depender de
    /// prefabs/cenas feitos a mao. Kit "9-slice" com sprite arredondado
    /// procedural, gradiente (sheen), cantos suaves, animacao de clique e
    /// suporte a imagens de Resources (icones, logos, thumbnails).
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
        private static Sprite _rounded;
        private static Sprite _circle;
        private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        /// <summary>Sprite arredondado gerado proceduralmente (9-slice, antialiased).</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = BuildRoundedSprite(64, 18);
                return _rounded;
            }
        }

        /// <summary>Circulo cheio antialiased (para pontos do minimapa, etc.).</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null) _circle = BuildCircleSprite(64);
                return _circle;
            }
        }

        private static Sprite BuildCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        // ---- Geracao do sprite arredondado --------------------------------

        private static Sprite BuildRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, radius, size - radius);
                    float cy = Mathf.Clamp(fy, radius, size - radius);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f); // borda AA de ~1px
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        // ---- Canvas / paineis / texto -------------------------------------

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(CanvasGroup), typeof(UIFadeIn));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Cria um RectTransform que preenche o pai mas se ajusta a Screen.safeArea
        /// (notch / home indicator). Use como raiz do conteudo da HUD.
        /// </summary>
        public static RectTransform SafeAreaRoot(Transform canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (RoundedSprite != null) { img.sprite = RoundedSprite; img.type = Image.Type.Sliced; }
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>
        /// Painel retangular puro (sem o sprite 9-slice arredondado). Ideal para
        /// barras de PREENCHIMENTO que escalam de 0 a 1: o sliced mostra um
        /// "sliver" dos cantos quando o fill fica bem estreito.
        /// </summary>
        public static RectTransform SolidPanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = Panel(parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero, color);
            var img = rt.GetComponent<Image>();
            img.sprite = null;
            img.type = Image.Type.Simple;
            return rt;
        }

        /// <summary>Sombra suave atras de um retangulo (deslocada). Crie ANTES do
        /// elemento para ela ficar atras.</summary>
        public static RectTransform Shadow(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offset, float alpha = 0.35f)
        {
            var rt = Panel(parent, anchorMin, anchorMax, offset, offset, new Color(0f, 0f, 0f, alpha));
            rt.GetComponent<Image>().raycastTarget = false;
            return rt;
        }

        /// <summary>Card premium: sombra + painel arredondado + borda clara sutil (glass).</summary>
        public static RectTransform Card(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color, bool shadow = true)
        {
            if (shadow) Shadow(parent, anchorMin, anchorMax, new Vector2(4f, -6f), 0.32f);
            var rt = Panel(parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero, color);
            var border = rt.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.10f);
            border.effectDistance = new Vector2(1f, 1f);
            return rt;
        }

        /// <summary>Linha divisoria fina (acento esportivo).</summary>
        public static RectTransform Divider(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject("Divider", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            SetRect(go, anchorMin, anchorMax);
            return go.GetComponent<RectTransform>();
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Titulos (fonte grande) ganham contorno escuro para nitidez sobre
            // fundos movimentados (substitui o ganho que o TMP daria).
            if (size >= 28)
            {
                var ol = go.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.55f);
                ol.effectDistance = new Vector2(1.4f, -1.4f);
            }
            return t;
        }

        // ---- Imagens de Resources (icones / logos / thumbnails) -----------

        private static Texture2D LoadTexture(string path)
        {
            if (_texCache.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            _texCache[path] = t;
            return t;
        }

        /// <summary>Sprite a partir de uma textura em Resources (cacheado). Null se nao existir.</summary>
        public static Sprite LoadSprite(string path)
        {
            if (_spriteCache.TryGetValue(path, out var s)) return s;
            var tex = LoadTexture(path);
            s = tex != null
                ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f)
                : null;
            _spriteCache[path] = s;
            return s;
        }

        private static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Icone de Resources/Icons/{key} (PNG transparente). Null se nao existir.</summary>
        public static Image Icon(Transform parent, string key, Vector2 aMin, Vector2 aMax, Color tint)
        {
            var sp = LoadSprite("Icons/" + key);
            if (sp == null) return null;
            var go = new GameObject("Icon", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.color = tint; img.preserveAspect = true; img.raycastTarget = false;
            SetRect(go, aMin, aMax);
            return img;
        }

        /// <summary>Logo da equipe de Resources/Logos/{teamId} (PNG transparente). Null se nao existir.</summary>
        public static Image Logo(Transform parent, string teamId, Vector2 aMin, Vector2 aMax)
        {
            var sp = LoadSprite("Logos/" + teamId);
            if (sp == null) return null;
            var go = new GameObject("Logo", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;
            SetRect(go, aMin, aMax);
            return img;
        }

        /// <summary>Imagem cheia (RawImage) de Resources/{resourcePath}; cor de fallback se faltar.</summary>
        public static RawImage Picture(Transform parent, string resourcePath, Vector2 aMin, Vector2 aMax, Color fallback)
        {
            var go = new GameObject("Picture", typeof(RawImage));
            go.transform.SetParent(parent, false);
            var ri = go.GetComponent<RawImage>();
            ri.raycastTarget = false;
            var tex = LoadTexture(resourcePath);
            if (tex != null) { ri.texture = tex; ri.color = Color.white; }
            else ri.color = fallback;
            SetRect(go, aMin, aMax);
            return ri;
        }

        /// <summary>
        /// Moldura com thumbnail do circuito (Resources/Thumbnails/{trackId}).
        /// As thumbnails sao SEMPRE quadradas (512x512). Para nunca distorcer, a
        /// moldura e mantida 1:1 e centralizada dentro do espaco reservado
        /// (AspectRatioFitter.FitInParent), independente do formato da caixa.
        /// Assim o tamanho fica padronizado em qualquer tela.
        /// </summary>
        public static void Thumbnail(Transform parent, string trackId, Vector2 aMin, Vector2 aMax)
        {
            // Container transparente que apenas delimita o espaco reservado.
            var holder = Panel(parent, aMin, aMax, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            holder.GetComponent<Image>().raycastTarget = false;

            // Moldura quadrada centralizada (mantem 1:1 dentro do holder).
            var frameGo = new GameObject("Thumb", typeof(Image), typeof(AspectRatioFitter));
            frameGo.transform.SetParent(holder, false);
            var frame = frameGo.GetComponent<Image>();
            frame.color = new Color(0.02f, 0.03f, 0.05f, 1f);
            frame.raycastTarget = false;
            var fitter = frameGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f; // thumbnails 512x512
            var border = frameGo.AddComponent<Outline>();
            border.effectColor = new Color(0.35f, 0.75f, 1f, 0.5f);
            border.effectDistance = new Vector2(2f, 2f);

            var pic = Picture(frame.transform, "Thumbnails/" + trackId, Vector2.zero, Vector2.one,
                new Color(0.12f, 0.14f, 0.20f, 1f));
            pic.rectTransform.offsetMin = new Vector2(4f, 4f);
            pic.rectTransform.offsetMax = new Vector2(-4f, -4f);
        }

        // ---- Fundo de tela -------------------------------------------------

        /// <summary>
        /// Fundo de tela. Tenta carregar Resources/Backgrounds/{key} e usa como
        /// imagem cheia; se nao existir, usa uma cor solida escura. Adiciona um
        /// leve overlay para legibilidade.
        /// </summary>
        public static void Background(Transform parent, string key, Color fallback)
        {
            var go = new GameObject("Background", typeof(RawImage));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling(); // fundo (indice 0)
            var ri = go.GetComponent<RawImage>();
            var rt = ri.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var tex = LoadTexture("Backgrounds/" + key);
            if (tex != null)
            {
                ri.texture = tex;
                ri.color = Color.white;
                var overlay = Panel(parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0f, 0f, 0f, 0.45f));
                overlay.SetSiblingIndex(1);
            }
            else
            {
                ri.color = fallback;
            }
        }

        // ---- Botoes --------------------------------------------------------

        public static Button Button(Transform parent, string text, Color bg,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(UIButtonFx));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = Color.white; // a cor vem do ColorBlock; branco deixa o gradiente limpo
            if (RoundedSprite != null) { img.sprite = RoundedSprite; img.type = Image.Type.Sliced; }

            // Sheen vertical sutil (claro em cima, levemente escuro embaixo).
            var grad = go.AddComponent<UIGradient>();
            grad.top = Color.white;
            grad.bottom = new Color(0.80f, 0.80f, 0.80f, 1f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = MakeColors(bg);

            // Borda neon sutil (estilo sci-fi).
            NeonBorder(go, MarbleUITheme.NeonCyan, 0.30f, 1.2f);

            Label(go.transform, text, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            return btn;
        }

        // ---- Design system premium (glass / neon / badge) ------------------

        /// <summary>Adiciona uma borda neon (glow) a um GameObject de UI.</summary>
        public static Outline NeonBorder(GameObject go, Color color, float alpha = 0.55f, float dist = 1.6f)
        {
            var ol = go.AddComponent<Outline>();
            var c = color; c.a = alpha; ol.effectColor = c;
            ol.effectDistance = new Vector2(dist, dist);
            return ol;
        }

        /// <summary>Painel glassmorphism: escuro translucido + borda neon ciano.</summary>
        public static RectTransform GlassPanel(Transform parent, Vector2 aMin, Vector2 aMax,
            Color? fill = null, Color? border = null)
        {
            var rt = Panel(parent, aMin, aMax, Vector2.zero, Vector2.zero, fill ?? MarbleUITheme.PanelDark);
            NeonBorder(rt.gameObject, border ?? MarbleUITheme.NeonCyan, 0.45f, 1.6f);
            return rt;
        }

        /// <summary>Badge circular de pneu (anel colorido + nucleo escuro + letra).
        /// Retorna o anel e a letra para atualizacao dinamica.</summary>
        public static (Image ring, Text letter) TyreBadge(Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var ringGo = new GameObject("TyreBadge", typeof(Image));
            ringGo.transform.SetParent(parent, false);
            var ring = ringGo.GetComponent<Image>();
            if (CircleSprite != null) ring.sprite = CircleSprite;
            ring.color = MarbleUITheme.TextSecondary;
            ring.raycastTarget = false;
            SetRect(ringGo, aMin, aMax);

            var coreGo = new GameObject("Core", typeof(Image));
            coreGo.transform.SetParent(ringGo.transform, false);
            var core = coreGo.GetComponent<Image>();
            if (CircleSprite != null) core.sprite = CircleSprite;
            core.color = new Color(0.04f, 0.07f, 0.11f, 1f);
            core.raycastTarget = false;
            var crt = coreGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.22f, 0.22f); crt.anchorMax = new Vector2(0.78f, 0.78f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            var letter = Label(ringGo.transform, "", 13, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            letter.fontStyle = FontStyle.Bold; letter.raycastTarget = false;
            return (ring, letter);
        }

        /// <summary>Badge de pneu estatico (estrategia/resultado).</summary>
        public static void TyreBadge(Transform parent, string gripId, Vector2 aMin, Vector2 aMax)
        {
            var b = TyreBadge(parent, aMin, aMax);
            MarbleUITheme.TyreInfo(gripId, out var l, out var c);
            b.ring.color = c; b.letter.text = l; b.letter.color = c;
        }

        /// <summary>Badge de pneu SEMPRE redondo (anchor central + lado fixo em px).
        /// Evita o circulo "oval" quando o rect do pai e largo/baixo.</summary>
        public static void TyreBadgeSquare(Transform parent, string letter, Color color, Vector2 center, float sizePx)
        {
            var b = TyreBadge(parent, center, center);
            b.ring.rectTransform.sizeDelta = new Vector2(sizePx, sizePx);
            b.ring.color = color; b.letter.text = letter; b.letter.color = color;
        }

        public static void TyreBadgeSquare(Transform parent, string gripId, Vector2 center, float sizePx)
        {
            MarbleUITheme.TyreInfo(gripId, out var l, out var c);
            TyreBadgeSquare(parent, l, c, center, sizePx);
        }

        /// <summary>ColorBlock coerente a partir da cor base do botao.</summary>
        public static ColorBlock MakeColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f),
                pressedColor = Color.Lerp(baseColor, Color.black, 0.25f),
                selectedColor = baseColor,
                disabledColor = new Color(baseColor.r * 0.4f, baseColor.g * 0.4f, baseColor.b * 0.4f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
        }

        /// <summary>Atualiza a cor base de um botao (mantendo estados/gradiente).</summary>
        public static void SetButtonColor(Button btn, Color baseColor)
        {
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = Color.white;
            btn.colors = MakeColors(baseColor);
        }
    }
}
