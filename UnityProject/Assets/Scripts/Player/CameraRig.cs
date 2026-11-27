using UnityEngine;

namespace Ares
{
    /// <summary>Smooth follow camera with aim lead, arena clamping, trauma shake and zoom.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public Transform Target;

        private Vector3 _vel;
        private float _trauma;
        private Vector2 _shakeOffset;
        private Vector2 _arenaMin;
        private Vector2 _arenaMax;
        public Vector2 MenuBias = new Vector2(-3.1f, 0.15f);
        private float _size = 5.9f;
        private float _targetSize = 5.9f;

        public float Size { get { return _size; } }

        public void Configure(Camera cam, Transform target, Vector2 arenaMin, Vector2 arenaMax, float size)
        {
            Cam = cam;
            Target = target;
            _arenaMin = arenaMin;
            _arenaMax = arenaMax;
            _size = size;
            _targetSize = size;
            if (Cam != null)
            {
                Cam.orthographic = true;
                Cam.orthographicSize = size;
            }
            SnapToTarget();
        }

        public void SetArena(Vector2 min, Vector2 max)
        {
            _arenaMin = min;
            _arenaMax = max;
        }

        public void SetSize(float size, bool instant)
        {
            _targetSize = Mathf.Max(0.5f, size);
            if (instant)
            {
                _size = _targetSize;
                if (Cam != null) Cam.orthographicSize = _size;
            }
        }

        public void SnapToTarget()
        {
            if (Target == null) return;
            Vector3 p = Target.position;
            transform.position = new Vector3(p.x, p.y, -10f);
            _vel = Vector3.zero;
            _trauma = 0f;
            _size = _targetSize;
            if (Cam != null) Cam.orthographicSize = _size;
        }

        public void AddShake(float amount) { _trauma = Mathf.Clamp01(_trauma + amount); }

        private void LateUpdate()
        {
            if (Cam == null) return;
            var cfg = Game.Cfg;
            float dt = Time.unscaledDeltaTime;

            _size = Mathf.Lerp(_size, _targetSize, Mathf.Clamp01(6f * dt));
            Cam.orthographicSize = _size;

            Vector3 desired;
            if (Target != null)
            {
                Vector2 aimLead = Vector2.zero;
                if (Game.Player != null && Game.Manager != null && Game.Manager.Phase == GamePhase.Playing)
                    aimLead = Game.Player.AimDir * Mathf.Min(cfg.cameraAimOffset, cfg.cameraMaxAimOffset);
                Vector3 tp = Target.position;
                Vector2 bias = (Game.Manager != null && Game.Manager.Phase == GamePhase.MainMenu) ? MenuBias : Vector2.zero;
                desired = new Vector3(tp.x + aimLead.x + bias.x, tp.y + aimLead.y + bias.y, -10f);
            }
            else desired = transform.position;

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref _vel, 1f / Mathf.Max(0.01f, cfg.cameraFollowLerp), Mathf.Infinity, dt);
            bool inMenu = Game.Manager != null && Game.Manager.Phase == GamePhase.MainMenu;
            if (!inMenu) smoothed = ClampToArena(smoothed);

            _trauma = Mathf.Max(0f, _trauma - cfg.cameraShakeDecay * dt);
            float shake = _trauma * _trauma;
            float amp = Mathf.Min(cfg.cameraShakeMax, shake * cfg.cameraShakeMax);
            float t = Time.unscaledTime * 24f;
            _shakeOffset = new Vector2((Mathf.PerlinNoise(t, 0.13f) - 0.5f) * 2f, (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f) * amp;

            transform.position = new Vector3(smoothed.x + _shakeOffset.x, smoothed.y + _shakeOffset.y, -10f);
        }

        private Vector3 ClampToArena(Vector3 p)
        {
            if (Cam == null) return p;
            float halfH = Cam.orthographicSize;
            float halfW = halfH * Cam.aspect;

            float minX = _arenaMin.x + halfW;
            float maxX = _arenaMax.x - halfW;
            float minY = _arenaMin.y + halfH;
            float maxY = _arenaMax.y - halfH;

            if (minX > maxX) p.x = (_arenaMin.x + _arenaMax.x) * 0.5f;
            else p.x = Mathf.Clamp(p.x, minX, maxX);

            if (minY > maxY) p.y = (_arenaMin.y + _arenaMax.y) * 0.5f;
            else p.y = Mathf.Clamp(p.y, minY, maxY);

            return p;
        }
    }
}
