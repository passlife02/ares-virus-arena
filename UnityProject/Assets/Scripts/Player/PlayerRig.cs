using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Ares
{
    /// <summary>
    /// Drives the upper body IK so the gun always points at the cursor while the legs keep
    /// playing the locomotion animation (feedback #10 / #17).
    ///
    /// Order matters: the aim target and weights are written in Update, before RigBuilder
    /// evaluates the playable graph; the gun orientation is written in LateUpdate, after the
    /// rig has already solved, so the barrel ends up exactly on the aim vector.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerRig : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public Transform AimTarget;
        public Transform AimHint;
        public Transform Shoulder;
        public Transform Hand;
        public Transform Gun;
        public Transform Muzzle;
        public GameObject SwordVisual;
        public GameObject BowVisual;
        public Rig AimRig;
        public TwoBoneIKConstraint ArmIk;

        [Header("Tuning")]
        public float aimDistanceFactor = 0.95f;
        public float weaponAimDistanceFactor = 1.55f;
        [Range(0f, 1f)] public float baseWeight = 1f;
        public float weightLerp = 14f;
        public float gunLerp = 30f;

        private float _weight;
        private float _targetWeight;
        private Vector2 _dir = Vector2.right;
        private float _reach = 0.7f;
        private float _gunAngle;
        private bool _initialised;

        public float CurrentWeight { get { return _weight; } }
        public Vector3 MuzzlePosition { get { return Muzzle != null ? Muzzle.position : transform.position; } }
        public bool HasRig { get { return ArmIk != null && AimTarget != null && Shoulder != null && Hand != null; } }

        private void Start()
        {
            Initialise();
        }

        public void Initialise()
        {
            if (_initialised) return;
            if (Shoulder != null && Hand != null)
            {
                float d = Vector3.Distance(Shoulder.position, Hand.position);
                _reach = Mathf.Clamp(d, 0.15f, 3f);
            }
            if (AimTarget != null) AimTarget.gameObject.SetActive(true);
            if (AimHint != null) AimHint.gameObject.SetActive(true);
            _gunAngle = 0f;
            _initialised = true;
        }

        /// <summary>Called every frame by the player controller.</summary>
        public void SetAim(Vector2 worldDir, float targetWeight)
        {
            if (worldDir.sqrMagnitude > 0.0001f) _dir = worldDir.normalized;
            _targetWeight = Mathf.Clamp01(targetWeight) * baseWeight;
        }

        private void UpdateHeldModel()
        {
            var eq = Game.Equipment;
            var def = eq != null ? eq.ActiveDef : null;
            bool melee = def != null && def.IsMelee;
            bool bow = def != null && def.IsRanged && def.fx == "feather";
            bool gun = !melee && !bow;

            if (Gun != null && Gun.gameObject.activeSelf != gun) Gun.gameObject.SetActive(gun);
            if (Gun != null)
            {
                // renderers too: the animator / equipment refresh can flip the object back on
                var gr = Gun.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < gr.Length; i++) if (gr[i] != null && gr[i].enabled != gun) gr[i].enabled = gun;
            }
            if (SwordVisual != null && SwordVisual.activeSelf != melee) SwordVisual.SetActive(melee);
            if (BowVisual != null && BowVisual.activeSelf != bow) BowVisual.SetActive(bow);
        }

        private void Update()
        {
            if (!_initialised) Initialise();
            UpdateHeldModel();
            float dt = Time.deltaTime;
            _weight = Mathf.MoveTowards(_weight, _targetWeight, weightLerp * dt);

            if (AimRig != null) AimRig.weight = _weight;
            if (ArmIk != null) ArmIk.weight = _weight;

            if (!HasRig) return;

            Vector3 shoulder = Shoulder.position;
            float dist = _reach * aimDistanceFactor;
            Vector3 target = shoulder + (Vector3)(_dir * dist);
            AimTarget.position = new Vector3(target.x, target.y, shoulder.z);

            if (AimHint != null)
            {
                // Keep the elbow out of the body: hint perpendicular to the aim, biased downward.
                Vector2 perp = new Vector2(-_dir.y, _dir.x);
                if (perp.y > 0f) perp = -perp;
                Vector3 hintOffset = (Vector3)(perp * (_reach * 0.9f)) + (Vector3)(_dir * (_reach * 0.25f));
                AimHint.position = shoulder + hintOffset;
            }
        }

        private void LateUpdate()
        {
            UpdateHeldModel();
            if (!_initialised || !HasRig) return;
            if (Gun == null) return;
            if (_weight < 0.02f) return;

            float want = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            _gunAngle = Mathf.LerpAngle(_gunAngle, want, Mathf.Clamp01(gunLerp * Time.deltaTime));
            Gun.rotation = Quaternion.Euler(0f, 0f, _gunAngle);
        }
    }
}
