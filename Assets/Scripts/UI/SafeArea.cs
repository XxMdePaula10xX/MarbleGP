using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Ajusta um RectTransform para caber dentro da Screen.safeArea (notch /
    /// Dynamic Island / cantos arredondados / home indicator do iPhone e iPad).
    /// Coloque todo o conteudo da HUD como filho deste RectTransform.
    /// Reaplica automaticamente quando a tela gira ou a resolucao muda.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafe;
        private Vector2Int _lastRes;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            // Forca o primeiro Update a aplicar. NAO aplicamos aqui de proposito:
            // Awake roda antes do SetParent/ancoragem do helper, entao deixamos o
            // primeiro Update (ja parenteado) cuidar disso, evitando ser
            // sobrescrito logo apos a criacao.
            _lastSafe = new Rect(-1f, -1f, -1f, -1f);
            _lastRes = Vector2Int.zero;
        }

        private void Update()
        {
            // Reaplica so quando algo muda (rotacao de tela, resize, multitasking).
            if (Screen.safeArea != _lastSafe ||
                Screen.width != _lastRes.x || Screen.height != _lastRes.y)
                Apply();
        }

        private void Apply()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;

            Rect sa = Screen.safeArea;
            _lastSafe = sa;
            _lastRes = new Vector2Int(w, h);

            Vector2 min = sa.position;             // canto inferior-esquerdo (px)
            Vector2 max = sa.position + sa.size;   // canto superior-direito (px)
            min.x /= w; min.y /= h;
            max.x /= w; max.y /= h;

            // Guarda contra valores invalidos (ex.: safeArea ainda nao resolvida).
            if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y))
                return;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
