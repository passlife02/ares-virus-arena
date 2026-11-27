using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Post-apocalyptic weather: a rain plane over the arena, splash bursts where drops land on
    /// water, drifting debris, and the odd lightning flash. Everything is built from the engine's
    /// own ParticleSystem so it stays in the Built-in pipeline.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public static Weather Instance { get; private set; }

        private ParticleSystem _rain;
        private ParticleSystem _rainFar;
        private ParticleSystem _rainNear;
        private ParticleSystem _splash;
        private Transform _follow;
        private SpriteRenderer _flash;
        private WaterSurface _water;

        private float _flashTimer;
        private float _nextLightning;
        private float _splashTimer;
        private float _density = 1f;       // 0..1, drives rain brightness and splash rate

        public static void Ensure(CameraRig cam, WaterSurface water)
        {
            if (cam == null) return;
            if (Instance != null)
            {
                Instance._follow = cam.transform;
                Instance._water = water;
                return;
            }
            var go = new GameObject("Weather");
            go.transform.SetParent(cam.transform, false);
            var w = go.AddComponent<Weather>();
            w._follow = cam.transform;
            w._water = water;
            w.Build();
            Instance = w;
        }

        public void SetWater(WaterSurface water) { _water = water; }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _rain = MakeSystem("Rain", ps => BuildRainLayer(ps, 420f, 0.55f, 0.95f, 11f, 15f, 0.5f, 0.8f, 1.0f, 900));
            _rainFar = MakeSystem("RainFar", ps => BuildRainLayer(ps, 260f, 0.75f, 1.3f, 6f, 9f, 0.26f, 0.42f, 0.55f, 700));
            _rainNear = MakeSystem("RainNear", ps => BuildRainLayer(ps, 120f, 0.35f, 0.6f, 17f, 22f, 1.0f, 1.6f, 1.35f, 500));
            _splash = MakeSystem("Splash", BuildSplash);

            var flashGo = new GameObject("LightningFlash");
            flashGo.transform.SetParent(transform, false);
            _flash = flashGo.AddComponent<SpriteRenderer>();
            _flash.sprite = Resources.Load<Sprite>("Weather/rain_streak");
            _flash.color = new Color(0.85f, 0.95f, 1f, 0f);
            _flash.sortingOrder = 950;

            _nextLightning = Random.Range(7f, 16f);
        }

        private ParticleSystem MakeSystem(string name, System.Action<ParticleSystem> configure)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            configure(ps);
            ps.Play();
            return ps;
        }

        private void BuildRainLayer(ParticleSystem ps, float rate, float lifeMin, float lifeMax,
                                    float speedMin, float speedMax, float sizeMin, float sizeMax,
                                    float alphaScale, int maxParticles)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new Color(0.78f, 0.88f, 0.94f, Mathf.Clamp01(0.95f * alphaScale));
            main.gravityModifier = 0.85f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 0.1f, 1f);
            shape.rotation = new Vector3(0f, 0f, -11f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.6f * alphaScale, -1.4f * alphaScale);
            vel.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.4f);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 26f;
            limit.dampen = 0.1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.80f, 0.88f, 0.92f), 0f),
                    new GradientColorKey(new Color(0.62f, 0.72f, 0.78f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.25f, 0f),
                    new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = grad;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            var tex = Resources.Load<Texture2D>("Weather/rain_streak");
            if (tex != null) rend.material.mainTexture = tex;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.045f;
            rend.lengthScale = 2.4f;
            rend.sortingOrder = alphaScale < 0.7f ? 870 : (alphaScale > 1.1f ? 910 : 900);
        }

        private void BuildRainDisabled(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(11f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            main.startColor = new Color(0.78f, 0.88f, 0.94f, 0.95f);   // cold, low saturation
            main.gravityModifier = 0.85f;
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 560f;

            // a flat plane across the arena, tilted a touch so the rain reads as wind-blown
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 0.1f, 1f);
            shape.rotation = new Vector3(0f, 0f, -11f);

            // wind push, slowly turning
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.6f, -1.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.4f);
            vel.yMultiplier = 1f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 26f;
            limit.dampen = 0.1f;

            // thin at the head, fading out along the fall
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.80f, 0.88f, 0.92f), 0f),
                    new GradientColorKey(new Color(0.62f, 0.72f, 0.78f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.25f, 0f),
                    new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = grad;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            var tex = Resources.Load<Texture2D>("Weather/rain_streak");
            if (tex != null) rend.material.mainTexture = tex;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.045f;
            rend.lengthScale = 2.4f;
            rend.sortingOrder = 900;
        }

        private void BuildSplash(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(0.82f, 0.92f, 0.96f, 0.9f);
            main.gravityModifier = 1.9f;
            main.maxParticles = 320;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;      // emitted by hand from the splash points

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // shoot up

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.92f, 0.98f, 1f), 0f),
                    new GradientColorKey(new Color(0.70f, 0.82f, 0.88f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.35f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = grad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            var tex = Resources.Load<Texture2D>("Weather/splash_drop");
            if (tex != null) rend.material.mainTexture = tex;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 880;
        }

        // ------------------------------------------------------------------ loop

        private void LateUpdate()
        {
            if (_follow == null) return;

            // keep the rain plane over the play area, just above the top of the view
            transform.position = _follow.position + new Vector3(0f, 9.5f, 0f);
            transform.rotation = Quaternion.identity;

            _density = Mathf.MoveTowards(_density, 1f, Time.deltaTime * 0.4f);

            // splashes only where there is water
            if (_water != null)
            {
                _splashTimer -= Time.deltaTime;
                if (_splashTimer <= 0f)
                {
                    _splashTimer = Random.Range(0.03f, 0.09f);
                    Vector2 p;
                    if (_water.TryRandomSurfacePoint(out p))
                    {
                        _splash.transform.position = new Vector3(p.x, p.y, 0f);
                        _splash.Emit(Random.Range(2, 5));
                    }
                }
            }

            _nextLightning -= Time.deltaTime;
            if (_nextLightning <= 0f)
            {
                _nextLightning = Random.Range(9f, 22f);
                _flashTimer = 0.30f;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(_flashTimer / 0.30f);
                var c = _flash.color;
                c.a = k * k * 0.18f;
                _flash.color = c;
                _flash.transform.position = _follow.position;
                _flash.transform.localScale = new Vector3(44f, 32f, 1f);
            }
            else if (_flash.color.a > 0f)
            {
                var c = _flash.color;
                c.a = 0f;
                _flash.color = c;
            }
        }
    }
}
