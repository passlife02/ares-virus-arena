using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Soft press feedback (feedback #6): hover fades a gradient in, pressing sinks and dims the
    /// plate, releasing gives a short bright pop. Driven by unscaled time so the pause menu and
    /// the paused backpack still feel alive.
    /// </summary>
    public class UiPressFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                               IPointerDownHandler, IPointerUpHandler
    {
        public Graphic Target;
        public RectTransform ScaleRoot;
        public CanvasGroup Fade;

        public Color Normal = new Color(0.20f, 0.22f, 0.23f, 0.95f);
        public Color Hover = new Color(0.34f, 0.40f, 0.28f, 1f);
        public Color Pressed = new Color(0.16f, 0.19f, 0.12f, 1f);
        public Color Flash = new Color(0.62f, 0.68f, 0.46f, 1f);

        public float hoverSpeed = 7f;
        public float pressSpeed = 18f;
        public float hoverScale = 0.018f;
        public float pressScale = 0.045f;
        public bool interactable = true;

        private float _hover;
        private float _press;
        private float _flash;
        private bool _hovering;
        private bool _held;

        public void OnPointerEnter(PointerEventData e) { if (interactable) _hovering = true; }
        public void OnPointerExit(PointerEventData e) { _hovering = false; _held = false; }
        public void OnPointerDown(PointerEventData e) { if (interactable) _held = true; }
        public void OnPointerUp(PointerEventData e)
        {
            if (_held)
            {
                _held = false;
                _flash = 1f;      // soft "click" pop
            }
        }

        private void OnDisable()
        {
            _hovering = false;
            _held = false;
            _hover = 0f;
            _press = 0f;
            _flash = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _hover = Mathf.MoveTowards(_hover, _hovering && interactable ? 1f : 0f, dt * hoverSpeed);
            _press = Mathf.MoveTowards(_press, _held && interactable ? 1f : 0f, dt * pressSpeed);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4.5f);

            if (Target != null)
            {
                Color c = Color.Lerp(Normal, Hover, _hover);
                c = Color.Lerp(c, Pressed, _press);
                if (_flash > 0f) c = Color.Lerp(c, Flash, _flash * 0.55f);
                Target.color = c;
            }

            if (ScaleRoot != null)
            {
                float s = 1f + _hover * hoverScale - _press * pressScale + _flash * 0.012f;
                ScaleRoot.localScale = new Vector3(s, s, 1f);
            }

            if (Fade != null) Fade.alpha = 0.86f + _hover * 0.14f;
        }
    }
}
