using UnityEngine;
using UnityEngine.EventSystems;

namespace MarbleGP.UI
{
    /// <summary>
    /// Animacao sutil de hover/clique (escala) para deixar os botoes "vivos".
    /// Usa tempo nao-escalado para continuar funcionando com o jogo pausado.
    /// </summary>
    public class UIButtonFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float Hover = 1.04f;
        private const float Press = 0.95f;

        private float _target = 1f;
        private bool _hover;

        private void OnEnable() { transform.localScale = Vector3.one; _target = 1f; _hover = false; }

        public void OnPointerEnter(PointerEventData e) { _hover = true; _target = Hover; }
        public void OnPointerExit(PointerEventData e) { _hover = false; _target = 1f; }
        public void OnPointerDown(PointerEventData e) { _target = Press; }
        public void OnPointerUp(PointerEventData e) { _target = _hover ? Hover : 1f; }

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * _target,
                Mathf.Clamp01(Time.unscaledDeltaTime * 14f));
        }
    }
}
