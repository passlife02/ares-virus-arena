using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Drag & drop for inventory grids (feedback #9 item 4): pick a stack up, drop it on another
    /// cell to swap or move it. A click still works as a fallback.
    /// </summary>
    public class UiDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int index;
        public bool isStash;
        public Sprite Icon;
        public Canvas Canvas;
        public System.Action<int, bool, int, bool> OnDropped;

        private static Image _ghost;

        public static void ResetGhost()
        {
            if (_ghost != null) Object.Destroy(_ghost.gameObject);
            _ghost = null;
        }

        private Image EnsureGhost()
        {
            if (_ghost != null) return _ghost;
            var go = new GameObject("DragGhost", typeof(RectTransform));
            go.transform.SetParent(Canvas.transform, false);
            _ghost = go.AddComponent<Image>();
            _ghost.raycastTarget = false;
            var crt = (RectTransform)go.transform;
            crt.sizeDelta = new Vector2(80f, 80f);
            return _ghost;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var model = isStash ? Game.Stash : Game.Inventory;
            if (model == null) return;
            var slot = model.Slots[index];
            if (slot.IsEmpty) return;

            var def = Game.Items != null ? Game.Items.Get(slot.itemId) : null;
            Icon = def != null ? Game.Items.GetIcon(def.icon) : null;

            var ghost = EnsureGhost();
            ghost.sprite = Icon;
            ghost.color = new Color(1f, 1f, 1f, 0.85f);
            ghost.enabled = true;
            ghost.transform.SetAsLastSibling();
            ghost.transform.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghost != null && _ghost.enabled) _ghost.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null) _ghost.enabled = false;
            if (OnDropped == null) return;

            // find which cell the pointer was released over
            var all = Object.FindObjectsOfType<UiDragSlot>();
            for (int i = 0; i < all.Length; i++)
            {
                var cell = all[i];
                if (cell == this) continue;
                var rt = (RectTransform)cell.transform;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, null)) continue;
                OnDropped(index, isStash, cell.index, cell.isStash);
                return;
            }
        }
    }
}
