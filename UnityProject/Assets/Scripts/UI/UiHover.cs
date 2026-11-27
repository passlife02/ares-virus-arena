using UnityEngine;
using UnityEngine.EventSystems;

namespace Ares
{
    /// <summary>Tiny hover proxy so plain images can report pointer enter / exit.</summary>
    public class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action onEnter;
        public System.Action onExit;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (onEnter != null) onEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (onExit != null) onExit();
        }
    }
}
