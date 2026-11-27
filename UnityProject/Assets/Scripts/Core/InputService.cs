using UnityEngine;
using UnityEngine.EventSystems;

namespace Ares
{
    /// <summary>
    /// Central input. Also owns the cursor policy requested in the feedback: while a run is
    /// active the pointer is locked inside the window, Esc releases it.
    /// </summary>
    public static class InputService
    {
        public static bool UiBlocking
        {
            get
            {
                if (Game.UiBlocking) return true;
                if (EventSystem.current == null) return false;
                return EventSystem.current.IsPointerOverGameObject();
            }
        }

        public static bool CursorLocked { get; private set; }

        public static void SetCursorLocked(bool locked)
        {
            CursorLocked = locked;
            Cursor.visible = !locked;
            Cursor.lockState = locked ? CursorLockMode.Confined : CursorLockMode.None;
        }

        public static Vector2 MoveAxis()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            Vector2 v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        public static bool FireHeld { get { return Input.GetMouseButton(0) && !UiBlocking; } }
        public static bool RollPressed { get { return Input.GetKeyDown(KeyCode.Space) && !UiBlocking; } }
        public static bool PickupPressed { get { return Input.GetKeyDown(KeyCode.F) && !UiBlocking; } }
        public static bool InventoryPressed { get { return Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I); } }
        public static bool PausePressed { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool SwitchWeaponPressed { get { return Input.GetMouseButtonDown(1); } }
        public static bool GrenadePressed { get { return Input.GetKeyDown(KeyCode.Q) && !UiBlocking; } }
        public static bool FencePressed { get { return Input.GetKeyDown(KeyCode.E) && !UiBlocking; } }

        public static Vector2 MouseScreen { get { return Input.mousePosition; } }
    }
}
