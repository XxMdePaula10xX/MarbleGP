using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Fade-in suave de um canvas/painel ao abrir (microinteracao). Usa tempo
    /// nao-escalado para funcionar tambem com o jogo pausado. Auto-desliga.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeIn : MonoBehaviour
    {
        public float duration = 0.22f;
        private CanvasGroup _cg;
        private float _t;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(_t / Mathf.Max(0.01f, duration));
            _cg.alpha = a;
            if (a >= 1f) { _cg.alpha = 1f; enabled = false; }
        }
    }
}
