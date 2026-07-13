using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Central wind model for the desert. Blends a slowly-varying base wind
    /// with two octaves of Perlin gusts and lets the wind direction drift
    /// over time, then drives the scene WindZone so everything already
    /// listening to it (TreeWindSway, particles, VFX) reacts for free.
    /// The WeatherDirector sets TargetStrength01; other systems read
    /// EffectiveStrength01 / Gust01 / WindDirection.
    /// </summary>
    [DisallowMultipleComponent]
    public class DesertWindController : MonoBehaviour
    {
        public static DesertWindController Instance { get; private set; }

        [Header("Wind zone")]
        [SerializeField] private WindZone m_WindZone;
        [SerializeField] private float m_CalmWindMain = 0.15f;
        [SerializeField] private float m_StormWindMain = 3.4f;
        [SerializeField, Range(0f, 1f)] private float m_TurbulenceRatio = 0.65f;

        [Header("Gusts")]
        [SerializeField] private float m_GustFrequency = 0.09f;
        [SerializeField, Range(0f, 1f)] private float m_GustWeight = 0.45f;

        [Header("Direction")]
        [SerializeField] private float m_StartYawDegrees = 235f;
        [SerializeField] private float m_DirectionDriftDegreesPerMinute = 30f;

        [Header("Response")]
        [SerializeField] private float m_StrengthRampSeconds = 14f;

        /// <summary>Set by the WeatherDirector; 0 = dead calm, 1 = full storm.</summary>
        public float TargetStrength01 { get; set; } = 0.15f;

        /// <summary>Smoothed base strength without gusts.</summary>
        public float Strength01 { get; private set; }

        /// <summary>Strength including gusts - what things visibly react to.</summary>
        public float EffectiveStrength01 { get; private set; }

        /// <summary>Current gust excitation, 0..1.</summary>
        public float Gust01 { get; private set; }

        public float WindYawDegrees => m_Yaw;
        public Vector3 WindDirection { get; private set; } = Vector3.forward;

        private float m_Yaw;
        private float m_Seed;

        private void Awake()
        {
            Instance = this;
            m_Yaw = m_StartYawDegrees;
            m_Seed = Random.Range(0f, 512f);
            if (m_WindZone == null)
            {
                m_WindZone = FindFirstObjectByType<WindZone>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            float ramp = m_StrengthRampSeconds > 0.01f ? dt / m_StrengthRampSeconds : 1f;
            Strength01 = Mathf.MoveTowards(Strength01, Mathf.Clamp01(TargetStrength01), ramp);

            // two noise octaves: slow swells plus faster flutter
            float slow = Mathf.PerlinNoise(m_Seed, t * m_GustFrequency);
            float fast = Mathf.PerlinNoise(m_Seed + 91f, t * m_GustFrequency * 4.7f);
            Gust01 = Mathf.Clamp01(slow * 0.75f + fast * 0.4f);

            EffectiveStrength01 = Mathf.Clamp01(
                Strength01 * Mathf.Lerp(1f, 0.35f + Gust01 * 1.35f, m_GustWeight));

            // the wind slowly veers; a strong wind holds its direction better
            float drift = (Mathf.PerlinNoise(m_Seed + 47f, t * 0.011f) - 0.5f) * 2f;
            m_Yaw += drift * m_DirectionDriftDegreesPerMinute * (1.15f - Strength01) * dt / 60f;
            WindDirection = Quaternion.Euler(0f, m_Yaw, 0f) * Vector3.forward;

            if (m_WindZone != null)
            {
                m_WindZone.transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
                float main = Mathf.Lerp(m_CalmWindMain, m_StormWindMain, EffectiveStrength01);
                m_WindZone.windMain = main;
                m_WindZone.windTurbulence = main * m_TurbulenceRatio * (0.5f + Gust01);
            }
        }
    }
}
