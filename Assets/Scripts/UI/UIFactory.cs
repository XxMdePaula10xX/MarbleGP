using UnityEngine;
using UnityEngine.UI;

namespace MarbleGP.UI
{
    /// <summary>
    /// Utilitarios para montar UI (uGUI legacy) por codigo, evitando depender
    /// de prefabs/cenas feitos a mao. Usa Text legacy para nao exigir TMP.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
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

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
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
            return t;
        }

        /// <summary>
        /// Fundo de tela. Tenta carregar Resources/Backgrounds/{key} (Texture2D
        /// importada como textura comum) e usa como imagem cheia; se nao existir,
        /// usa uma cor solida escura. Adiciona um leve overlay para legibilidade.
        /// Assim o jogador pode gerar artes e soltar em Assets/Resources/Backgrounds/.
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

            var tex = Resources.Load<Texture2D>("Backgrounds/" + key);
            if (tex != null)
            {
                ri.texture = tex;
                ri.color = Color.white;
                // Overlay escuro logo acima da imagem (indice 1) para legibilidade.
                var overlay = Panel(parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0f, 0f, 0f, 0.45f));
                overlay.SetSiblingIndex(1);
            }
            else
            {
                ri.color = fallback;
            }
        }

        public static Button Button(Transform parent, string text, Color bg,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var label = Label(go.transform, text, 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);
            return go.GetComponent<Button>();
        }
    }
}
