using UnityEngine;

namespace Ares
{
    /// <summary>A dropped item lying in the world. Never self-collects.</summary>
    public class LootPickup : MonoBehaviour
    {
        public SpriteRenderer Icon;
        public SpriteRenderer Glow;

        public string ItemId { get; private set; }
        public int Count { get; private set; }
        public ItemDef Def { get; private set; }

        private ItemDatabase _db;
        private float _bobPhase;
        private float _spawnTime;
        private Vector3 _basePos;

        public void Setup(ItemDef def, int count, ItemDatabase db)
        {
            Def = def;
            Count = count;
            ItemId = def != null ? def.id : null;
            _db = db;
            _bobPhase = Random.Range(0f, 6.28f);
            _spawnTime = Time.time;
            _basePos = transform.position;

            if (Icon != null)
            {
                Icon.sprite = db != null ? db.GetIcon(def.icon) : null;
                Icon.enabled = Icon.sprite != null;
                float target = def != null && def.IsWeapon ? 0.95f : 0.72f;
                if (Icon.sprite != null)
                {
                    float maxDim = Mathf.Max(Icon.sprite.bounds.size.x, Icon.sprite.bounds.size.y);
                    Icon.transform.localScale = Vector3.one * (maxDim > 0.001f ? target / maxDim : 1f);
                }
                Icon.color = def != null && def.IsWeapon ? new Color(1f, 0.95f, 0.78f) : Color.white;
            }
            if (Glow != null)
            {
                Glow.enabled = true;
                Glow.color = new Color(1f, 0.88f, 0.45f, 0.45f);
            }
        }

        private void Update()
        {
            if (Icon == null) return;
            float t = Time.time - _spawnTime;
            float bob = Mathf.Sin(Time.time * 3.2f + _bobPhase) * 0.06f;
            float pop = Mathf.Clamp01(t / 0.25f);
            transform.position = new Vector3(_basePos.x, _basePos.y + 0.18f + bob, 0f);
            if (Glow != null)
            {
                Color c = Glow.color;
                c.a = (0.55f + Mathf.Sin(Time.time * 4f + _bobPhase) * 0.12f) * pop;
                Glow.color = c;
            }
            transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
        }

        public void Consume(int amount)
        {
            Count -= amount;
            if (Count <= 0)
            {
                if (_db != null) _db.RemovePickup(this);
                else Destroy(gameObject);
            }
        }
    }
}
