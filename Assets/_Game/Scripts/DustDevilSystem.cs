using System.Collections.Generic;
using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Dust devils for hot, calm, clear hours. When the AirTemperatureModel
    /// reports real heat and neither rain nor a sandstorm is active, small
    /// spinning columns of sand spawn around the main camera and wander
    /// across the terrain, nudged along the prevailing wind. Each devil is a
    /// code-built Shuriken system (cone emitter + orbital velocity for the
    /// spin); per-particle alpha is impossible with HDRP/Unlit, so particles
    /// fade by growing in and shrinking out via size-over-lifetime instead.
    /// The shared dust material is never touched - a private instance is
    /// tinted by daylight so the unlit dust cannot glow at night.
    /// </summary>
    [DisallowMultipleComponent]
    public class DustDevilSystem : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Terrain m_Terrain;
        [SerializeField] private AirTemperatureModel m_Temperature;
        [SerializeField] private WeatherSystem m_Weather;
        [SerializeField] private SandstormSystem m_Sandstorm;
        [SerializeField] private DesertWindController m_Wind;
        [SerializeField] private DayNightCycle m_DayNight;

        [Header("Look")]
        [SerializeField] private Material m_DustMaterial;
        [SerializeField] private Color m_DustTint = new Color(0.80f, 0.64f, 0.44f, 0.42f);

        [Header("Spawning")]
        [SerializeField, Range(0, 4)] private int m_MaxDevils = 2;
        [Tooltip("Average seconds between spawns when conditions are perfect.")]
        [SerializeField] private float m_MeanSpawnSeconds = 45f;
        [Tooltip("AirTemperatureModel.Heat01 needed before devils can form.")]
        [SerializeField, Range(0f, 1f)] private float m_MinHeat = 0.55f;
        [SerializeField] private Vector2 m_SpawnDistanceRange = new Vector2(45f, 160f);
        [SerializeField] private Vector2 m_LifeSecondsRange = new Vector2(28f, 65f);
        [Tooltip("Ground below this world height is off limits - keeps devils out of the lake basin.")]
        [SerializeField] private float m_MinWorldY = 3f;

        [Header("Shape and motion")]
        [SerializeField] private float m_ColumnHeight = 20f;
        [SerializeField] private float m_SpinVelocity = 6.5f;
        [SerializeField] private float m_EmissionRate = 110f;
        [SerializeField] private Vector2 m_WanderSpeedRange = new Vector2(1.5f, 4f);

        public int ActiveCount => m_Devils.Count;

        private static readonly int s_UnlitColorId = Shader.PropertyToID("_UnlitColor");

        private class Devil
        {
            public GameObject go;
            public ParticleSystem ps;
            public float age;
            public float life;
            public float speed;
            public float seed;
        }

        private readonly List<Devil> m_Devils = new List<Devil>();
        private Material m_MatInstance;

        private void Awake()
        {
            if (m_Terrain == null) m_Terrain = Terrain.activeTerrain;
            if (m_Temperature == null) m_Temperature = FindFirstObjectByType<AirTemperatureModel>();
            if (m_Weather == null) m_Weather = FindFirstObjectByType<WeatherSystem>();
            if (m_Sandstorm == null) m_Sandstorm = FindFirstObjectByType<SandstormSystem>();
            if (m_Wind == null) m_Wind = FindFirstObjectByType<DesertWindController>();
            if (m_DayNight == null) m_DayNight = FindFirstObjectByType<DayNightCycle>();
        }

        private void Start()
        {
            if (m_DustMaterial == null)
            {
                Debug.LogWarning("[DustDevilSystem] No dust material assigned - disabled.", this);
                enabled = false;
                return;
            }
            m_MatInstance = new Material(m_DustMaterial);
        }

        private void OnDestroy()
        {
            if (m_MatInstance != null)
            {
                Destroy(m_MatInstance);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool allowed = ConditionsAllow(out float heat01);

            if (m_MatInstance != null && m_DayNight != null)
            {
                float sun = Mathf.Lerp(0.06f, 1f, m_DayNight.DaylightFactor);
                Color c = m_DustTint;
                m_MatInstance.SetColor(s_UnlitColorId, new Color(c.r * sun, c.g * sun, c.b * sun, c.a));
            }

            if (allowed && m_Devils.Count < m_MaxDevils && m_MeanSpawnSeconds > 1f)
            {
                // hotter afternoons brew devils more often
                float k = Mathf.InverseLerp(m_MinHeat, 1f, heat01);
                if (Random.value < dt / m_MeanSpawnSeconds * (0.35f + 0.65f * k))
                {
                    SpawnDevil();
                }
            }

            for (int i = m_Devils.Count - 1; i >= 0; i--)
            {
                if (!StepDevil(m_Devils[i], dt, allowed))
                {
                    m_Devils.RemoveAt(i);
                }
            }
        }

        private bool ConditionsAllow(out float heat01)
        {
            heat01 = m_Temperature != null
                ? m_Temperature.Heat01
                : (m_DayNight != null ? m_DayNight.DaylightFactor : 1f);
            float storm = m_Sandstorm != null ? m_Sandstorm.Intensity01 : 0f;
            float rain = m_Weather != null
                ? Mathf.Max(m_Weather.RainIntensity01, m_Weather.Wetness01)
                : 0f;
            return heat01 >= m_MinHeat && storm < 0.15f && rain < 0.08f;
        }

        [ContextMenu("Spawn Dust Devil Now")]
        public void SpawnDevil()
        {
            if (m_MatInstance == null)
            {
                return;
            }

            Camera cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 pos = origin;
            bool found = false;
            for (int attempt = 0; attempt < 20 && !found; attempt++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(m_SpawnDistanceRange.x, m_SpawnDistanceRange.y);
                pos = origin + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                found = OnTerrain(ref pos);
            }
            if (!found)
            {
                return;
            }

            var go = new GameObject("DustDevil (runtime)");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;

            ParticleSystem ps = BuildColumn(go);
            m_Devils.Add(new Devil
            {
                go = go,
                ps = ps,
                life = Random.Range(m_LifeSecondsRange.x, m_LifeSecondsRange.y),
                speed = Random.Range(m_WanderSpeedRange.x, m_WanderSpeedRange.y),
                seed = Random.Range(0f, 512f)
            });
            ps.Play();
        }

        /// <summary>Clamps a point onto the terrain; false near/off the edge
        /// or below m_MinWorldY (the lake basin).</summary>
        private bool OnTerrain(ref Vector3 pos)
        {
            if (m_Terrain == null)
            {
                return false;
            }
            TerrainData data = m_Terrain.terrainData;
            Vector3 tp = m_Terrain.transform.position;
            float nx = Mathf.InverseLerp(tp.x, tp.x + data.size.x, pos.x);
            float nz = Mathf.InverseLerp(tp.z, tp.z + data.size.z, pos.z);
            if (nx < 0.05f || nx > 0.95f || nz < 0.05f || nz > 0.95f)
            {
                return false;
            }
            float y = m_Terrain.SampleHeight(pos) + tp.y;
            if (y < m_MinWorldY)
            {
                return false;
            }
            pos.y = y;
            return true;
        }

        private ParticleSystem BuildColumn(GameObject go)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            float rise = m_ColumnHeight / 3f; // particles reach the top in ~3 s
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.15f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.75f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = m_SpinVelocity;   // the spin
            vel.radial = 0.5f;               // widens toward the top

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.25f), new Keyframe(0.2f, 0.75f),
                new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.75f;
            noise.frequency = 0.35f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = m_MatInstance;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return ps;
        }

        private bool StepDevil(Devil d, float dt, bool allowed)
        {
            if (d.go == null)
            {
                return false;
            }

            d.age += dt;
            // bad weather kills a devil early: jump straight to the fade-out
            if (!allowed && d.age < d.life - 6f)
            {
                d.age = d.life - 6f;
            }

            if (d.age >= d.life)
            {
                d.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                Destroy(d.go, 4f);
                return false;
            }

            float fadeIn = Mathf.Clamp01(d.age / 5f);
            float fadeOut = Mathf.Clamp01((d.life - d.age) / 6f);
            float strength = Mathf.Min(fadeIn, fadeOut);

            ParticleSystem.EmissionModule emission = d.ps.emission;
            emission.rateOverTime = m_EmissionRate * strength;

            // wander: perlin heading nudged along the prevailing wind
            float perlinYaw = (Mathf.PerlinNoise(d.seed, Time.time * 0.05f) - 0.5f) * 720f;
            float windYaw = m_Wind != null ? m_Wind.WindYawDegrees : perlinYaw;
            float yaw = Mathf.LerpAngle(perlinYaw, windYaw, 0.45f);
            Vector3 step = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (d.speed * dt);

            Vector3 pos = d.go.transform.position + step;
            if (!OnTerrain(ref pos))
            {
                // blocked by the map edge or the lake - steer back toward
                // the middle of the terrain instead of dying at the border
                Vector3 tp = m_Terrain != null ? m_Terrain.transform.position : Vector3.zero;
                Vector3 ts = m_Terrain != null ? m_Terrain.terrainData.size : Vector3.zero;
                Vector3 toCentre = new Vector3(tp.x + ts.x * 0.5f, 0f, tp.z + ts.z * 0.5f)
                                   - d.go.transform.position;
                toCentre.y = 0f;
                pos = d.go.transform.position + toCentre.normalized * (d.speed * dt);
                if (!OnTerrain(ref pos))
                {
                    // truly stuck - fade out where it stands
                    d.age = Mathf.Max(d.age, d.life - 6f);
                    return true;
                }
            }
            d.go.transform.position = pos;
            return true;
        }
    }
}
