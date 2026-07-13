using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Ground-hugging blowing sand (saltation). A single camera-following
    /// particle box streams stretched sand motes along the wind once the
    /// DesertWindController passes a threshold - the visual link between a
    /// simple breeze and the full SandstormSystem fog soup. Built entirely
    /// in code at Start. HDRP/Unlit ignores per-particle vertex alpha, so
    /// motes fade by size-over-lifetime, and a private material instance is
    /// tinted by daylight so the unlit sand never glows at night.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlowingSandSystem : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Terrain m_Terrain;
        [SerializeField] private DesertWindController m_Wind;
        [SerializeField] private DayNightCycle m_DayNight;

        [Header("Look")]
        [SerializeField] private Material m_SandMaterial;
        [SerializeField] private Color m_SandTint = new Color(0.85f, 0.70f, 0.50f, 0.5f);

        [Header("Behaviour")]
        [Tooltip("Wind strength (EffectiveStrength01) where sand starts to lift.")]
        [SerializeField, Range(0f, 1f)] private float m_WindThreshold = 0.32f;
        [SerializeField] private float m_MaxEmissionRate = 650f;
        [SerializeField] private Vector2 m_SpeedRange = new Vector2(7f, 22f);
        [SerializeField] private float m_AreaSize = 60f;
        [Tooltip("The emitter never sinks below this world height - keeps sand above the lake surface.")]
        [SerializeField] private float m_MinWorldY = 2.5f;

        public float Strength01 { get; private set; }

        private static readonly int s_UnlitColorId = Shader.PropertyToID("_UnlitColor");

        private ParticleSystem m_Ps;
        private ParticleSystem.VelocityOverLifetimeModule m_Vel;
        private Transform m_PsTransform;
        private Material m_MatInstance;

        private void Awake()
        {
            if (m_Terrain == null) m_Terrain = Terrain.activeTerrain;
            if (m_Wind == null) m_Wind = FindFirstObjectByType<DesertWindController>();
            if (m_DayNight == null) m_DayNight = FindFirstObjectByType<DayNightCycle>();
        }

        private void Start()
        {
            if (m_SandMaterial == null)
            {
                Debug.LogWarning("[BlowingSandSystem] No sand material assigned - disabled.", this);
                enabled = false;
                return;
            }
            m_MatInstance = new Material(m_SandMaterial);
            BuildParticles();
        }

        private void OnDestroy()
        {
            if (m_MatInstance != null)
            {
                Destroy(m_MatInstance);
            }
        }

        private void BuildParticles()
        {
            var go = new GameObject("BlowingSand (runtime)");
            go.transform.SetParent(transform, false);
            m_PsTransform = go.transform;

            m_Ps = go.AddComponent<ParticleSystem>();
            m_Ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = m_Ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.maxParticles = 1600;
            main.gravityModifier = 0.12f;

            ParticleSystem.EmissionModule emission = m_Ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = m_Ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(m_AreaSize, 1.2f, m_AreaSize);

            m_Vel = m_Ps.velocityOverLifetime;
            m_Vel.enabled = true;
            m_Vel.space = ParticleSystemSimulationSpace.World;

            ParticleSystem.SizeOverLifetimeModule size = m_Ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.12f, 1f),
                new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.NoiseModule noise = m_Ps.noise;
            noise.enabled = true;
            noise.strength = 1f;
            noise.frequency = 0.7f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 5f;
            rend.velocityScale = 0.03f;
            rend.sharedMaterial = m_MatInstance;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            m_Ps.Play();
        }

        private void Update()
        {
            if (m_Ps == null)
            {
                return;
            }

            float wind = m_Wind != null ? m_Wind.EffectiveStrength01 : 0f;
            Strength01 = Mathf.InverseLerp(m_WindThreshold, 0.95f, wind);

            if (m_MatInstance != null && m_DayNight != null)
            {
                float sun = Mathf.Lerp(0.06f, 1f, m_DayNight.DaylightFactor);
                Color c = m_SandTint;
                m_MatInstance.SetColor(s_UnlitColorId, new Color(c.r * sun, c.g * sun, c.b * sun, c.a));
            }

            ParticleSystem.EmissionModule emission = m_Ps.emission;
            emission.rateOverTime = m_MaxEmissionRate * Mathf.Pow(Strength01, 1.3f);

            if (Strength01 <= 0f)
            {
                return; // existing motes just die out
            }

            // stream along the current wind, with some per-mote spread
            float speed = Mathf.Lerp(m_SpeedRange.x, m_SpeedRange.y, Strength01);
            Vector3 dir = m_Wind != null ? m_Wind.WindDirection : Vector3.forward;
            m_Vel.x = new ParticleSystem.MinMaxCurve(dir.x * speed * 0.8f, dir.x * speed * 1.2f);
            m_Vel.z = new ParticleSystem.MinMaxCurve(dir.z * speed * 0.8f, dir.z * speed * 1.2f);
            m_Vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.7f);

            // keep the emitter box on the ground under the camera, shifted
            // upwind so the streaks blow across the viewer
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                float ground = p.y - 12f;
                if (m_Terrain != null)
                {
                    ground = m_Terrain.SampleHeight(p) + m_Terrain.transform.position.y;
                }
                ground = Mathf.Max(ground, m_MinWorldY);
                m_PsTransform.position = new Vector3(p.x, ground + 0.8f, p.z) - dir * (m_AreaSize * 0.2f);
            }
        }
    }
}
