using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Shows the melee reach as a faint arc around the player and flashes it on every swing,
    /// so the attack range is legible before and during the hit (feedback: "近战特效不明显,
    /// 最好有范围显示").
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class MeleeArcView : MonoBehaviour
    {
        public SpriteRenderer Arc;
        public Transform Owner;

        public float idleAlpha = 0f;
        public float swingAlpha = 0.62f;
        public float fadeSpeed = 9f;

        private float _alpha;

        private void LateUpdate()
        {
            if (Arc == null || Owner == null) return;

            var eq = Game.Equipment;
            var def = eq != null ? eq.ActiveDef : null;
            bool unarmed = def == null;   // fists show reach too
            var player = Game.Player;
            var weapons = player != null ? player.Weapons : null;

            bool show = Game.CanPlay && (unarmed || (def != null && def.IsMelee)) && player != null && player.IsAlive && !player.IsRolling;
            float range = def != null && def.range > 0.1f ? def.range : 1.4f;
            if (unarmed) range = 1.05f;   // matches the fist reach in PlayerCombat

            // charging draws the arc at the charged reach, brighter as it builds (feedback #14 item 4)
            bool charging = weapons != null && weapons.Charging;
            if (charging) range = Mathf.Max(range, weapons.ChargedRange);

            float target = 0f;
            if (show)
            {
                target = idleAlpha;
                if (weapons != null && weapons.Swinging)
                    target = Mathf.Lerp(swingAlpha, idleAlpha, weapons.SwingProgress);
                if (charging)
                    target = Mathf.Lerp(0.22f, swingAlpha, weapons.Charge01);
                else if (InputService.FireHeld) target = Mathf.Max(target, idleAlpha * 1.8f);
            }

            _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);
            if (Arc.enabled != (_alpha > 0.005f)) Arc.enabled = _alpha > 0.005f;
            if (!Arc.enabled) return;

            Vector2 dir = weapons != null && weapons.Swinging ? weapons.SwingDir : (player != null ? player.AimDir : Vector2.right);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Arc.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            Arc.transform.position = Owner.position;

            float s = range * 2.15f;
            Arc.transform.localScale = new Vector3(s, s, 1f);

            Color c = Arc.color;
            c.a = _alpha;
            Arc.color = c;
        }
    }
}
