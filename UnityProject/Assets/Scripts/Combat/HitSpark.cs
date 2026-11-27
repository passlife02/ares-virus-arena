using UnityEngine;

namespace Ares
{
    /// <summary>Short lived pooled sprite used for impacts, blood and muzzle flashes.</summary>
    public class HitSpark : MonoBehaviour
    {
        public SpriteRenderer Renderer;

        private float _life;
        private float _maxLife;
        private float _startScale;
        private float _endScale;
        private Color _color;

        public void Play(Vector2 pos, Color color, float startScale, float endScale, float life)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
            _color = color;
            _startScale = startScale;
            _endScale = endScale;
            _maxLife = Mathf.Max(0.01f, life);
            _life = _maxLife;
            if (Renderer != null)
            {
                Renderer.color = color;
                Renderer.enabled = true;
            }
            transform.localScale = Vector3.one * startScale;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            float t = 1f - _life / _maxLife;
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
            if (Renderer != null)
            {
                Color c = _color;
                c.a = _color.a * (1f - t);
                Renderer.color = c;
            }
        }
    }
}
