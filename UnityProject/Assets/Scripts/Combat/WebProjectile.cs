using UnityEngine;

namespace Ares
{
    /// <summary>Spider boss web shot: on hit the player is stuck for a moment.</summary>
    public class WebProjectile : MonoBehaviour
    {
        private Vector2 _pos;
        private Vector2 _dir;
        private float _speed;
        private float _life;
        private float _root;

        public void Launch(Vector2 pos, Vector2 dir, float speed, float life, float rootSeconds)
        {
            _pos = pos;
            _dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
            _speed = speed;
            _life = life;
            _root = rootSeconds;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.localScale = Vector3.one * 0.32f;

            var sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Fx.Get("fx_web");
            sr.sortingOrder = 45;
            sr.color = new Color(0.92f, 0.88f, 0.98f, 0.9f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Burst(false); return; }

            transform.Rotate(0f, 0f, 240f * dt);

            float step = _speed * dt;
            RaycastHit2D wall = Physics2D.CircleCast(_pos, 0.22f, _dir, step, Motion.WallMask);
            if (wall.collider != null) { _pos += _dir * Mathf.Max(0f, wall.distance - 0.02f); Burst(false); return; }

            _pos += _dir * step;
            transform.position = new Vector3(_pos.x, _pos.y, 0f);

            var player = Game.Player;
            if (player != null && player.IsAlive && (player.Position - _pos).magnitude <= 0.62f)
            {
                player.Root(_root);
                Burst(true);
                return;
            }
        }

        private void Burst(bool hitPlayer)
        {
            Fx.Play("fx_web", _pos, hitPlayer ? 1.5f : 0.9f, 0.7f);
            if (hitPlayer)
            {
                AudioManager.PlaySfx("sfx_hurt", 0.5f, 0.2f);
                if (Game.Hud != null) Game.Hud.ShowPrompt("被蛛丝缠住了！", 1.2f);
            }
            Destroy(gameObject);
        }
    }
}
