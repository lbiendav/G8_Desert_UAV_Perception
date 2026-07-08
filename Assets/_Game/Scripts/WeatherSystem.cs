using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Rain state machine + wet-surface look. While raining, a camera-following
    /// particle system emits rain streaks and surfaces get progressively wet:
    /// terrain layers darken and gain smoothness (glossy wet sheen), tree
    /// materials gain smoothness. When rain stops, everything dries out slowly
    /// (faster under a high sun).
    ///
    /// The wet look works by editing shared TerrainLayer/Material assets at
    /// runtime, so the pristine values are cached on enable and restored on
    /// disable - assets are never permanently altered.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Cycle")]
        [SerializeField] private bool m_AutoCycle = true;
        [SerializeField] private Vector2 m_DrySecondsRange = new Vector2(70f, 160f);
        [SerializeField] private Vector2 m_RainSecondsRange = new Vector2(30f, 70f);
        [SerializeField] private float m_RainRampSeconds = 6f;

        [Header("Rain particles")]
        [SerializeField] private ParticleSystem m_RainParticles;
        [SerializeField] private float m_MaxRainRate = 2200f;
        [SerializeField] private Transform m_FollowTarget;
        [SerializeField] private Vector3 m_FollowOffset = new Vector3(0f, 28f, 0f);

        [Header("Wetness")]
        [SerializeField] private float m_SoakSeconds = 10f;
        [SerializeField] private float m_DryOutSeconds = 35f;
        [SerializeField] private DayNightCycle m_DayNight;
        [SerializeField] private TerrainLayer m_SandLayer;
        [SerializeField] private TerrainLayer m_RockLayer;
        [SerializeField] private Material[] m_WetterMaterials;
        [SerializeField] private float m_WetSmoothnessSand = 0.55f;
        [SerializeField] private float m_WetSmoothnessRock = 0.45f;
        [SerializeField] private Color m_WetTintSand = new Color(0.52f, 0.50f, 0.48f, 1f);
        [SerializeField] private Color m_WetTintRock = new Color(0.58f, 0.58f, 0.60f, 1f);
        [SerializeField] private float m_MaterialSmoothnessBoost = 0.3f;

        public bool IsRaining { get; private set; }
        public float RainIntensity01 { get; private set; }
        public float Wetness01 { get; private set; }

        private float m_PhaseTimer;

        private float m_SandSmooth0;
        private float m_RockSmooth0;
        private Color m_SandRemap0;
        private Color m_RockRemap0;
        private float[] m_MatSmooth0;

        private void OnEnable()
        {
            if (m_SandLayer != null)
            {
                m_SandSmooth0 = m_SandLayer.smoothness;
                m_SandRemap0 = m_SandLayer.diffuseRemapMax;
            }
            if (m_RockLayer != null)
            {
                m_RockSmooth0 = m_RockLayer.smoothness;
                m_RockRemap0 = m_RockLayer.diffuseRemapMax;
            }
            if (m_WetterMaterials != null)
            {
                m_MatSmooth0 = new float[m_WetterMaterials.Length];
                for (int i = 0; i < m_WetterMaterials.Length; i++)
                {
                    Material m = m_WetterMaterials[i];
                    m_MatSmooth0[i] = (m != null && m.HasProperty("_Smoothness")) ? m.GetFloat("_Smoothness") : 0f;
                }
            }

            m_PhaseTimer = Random.Range(m_DrySecondsRange.x, m_DrySecondsRange.y) * 0.5f;
        }

        private void OnDisable()
        {
            // put the shared assets back exactly as they were
            if (m_SandLayer != null)
            {
                m_SandLayer.smoothness = m_SandSmooth0;
                m_SandLayer.diffuseRemapMax = m_SandRemap0;
            }
            if (m_RockLayer != null)
            {
                m_RockLayer.smoothness = m_RockSmooth0;
                m_RockLayer.diffuseRemapMax = m_RockRemap0;
            }
            if (m_WetterMaterials != null && m_MatSmooth0 != null)
            {
                for (int i = 0; i < m_WetterMaterials.Length; i++)
                {
                    Material m = m_WetterMaterials[i];
                    if (m != null && m.HasProperty("_Smoothness"))
                    {
                        m.SetFloat("_Smoothness", m_MatSmooth0[i]);
                    }
                }
            }
        }

        /// <summary>Force rain on/off (also usable from other scripts or UI).</summary>
        public void SetRaining(bool raining)
        {
            IsRaining = raining;
            m_PhaseTimer = raining
                ? Random.Range(m_RainSecondsRange.x, m_RainSecondsRange.y)
                : Random.Range(m_DrySecondsRange.x, m_DrySecondsRange.y);
        }

        private void Update()
        {
            if (m_AutoCycle)
            {
                m_PhaseTimer -= Time.deltaTime;
                if (m_PhaseTimer <= 0f)
                {
                    SetRaining(!IsRaining);
                }
            }

            float target = IsRaining ? 1f : 0f;
            RainIntensity01 = Mathf.MoveTowards(RainIntensity01, target, Time.deltaTime / Mathf.Max(m_RainRampSeconds, 0.01f));

            if (m_RainParticles != null)
            {
                ParticleSystem.EmissionModule em = m_RainParticles.emission;
                em.rateOverTime = m_MaxRainRate * RainIntensity01;
            }

            float daylight = 1f;
            if (m_DayNight != null)
            {
                daylight = m_DayNight.DaylightFactor;
                // overcast: rain clouds swallow most of the direct sunlight
                m_DayNight.SunMultiplier = 1f - 0.72f * RainIntensity01;
            }
            if (RainIntensity01 > 0.01f)
            {
                Wetness01 = Mathf.Min(1f, Wetness01 + Time.deltaTime / m_SoakSeconds * RainIntensity01);
            }
            else
            {
                Wetness01 = Mathf.Max(0f, Wetness01 - Time.deltaTime / m_DryOutSeconds * (0.35f + daylight));
            }

            ApplyWetness(Wetness01);
        }

        private void LateUpdate()
        {
            if (m_FollowTarget != null && m_RainParticles != null)
            {
                m_RainParticles.transform.position = m_FollowTarget.position + m_FollowOffset;
            }
        }

        private void ApplyWetness(float w)
        {
            if (m_SandLayer != null)
            {
                m_SandLayer.smoothness = Mathf.Lerp(m_SandSmooth0, m_WetSmoothnessSand, w);
                m_SandLayer.diffuseRemapMax = Color.Lerp(m_SandRemap0, m_WetTintSand, w);
            }
            if (m_RockLayer != null)
            {
                m_RockLayer.smoothness = Mathf.Lerp(m_RockSmooth0, m_WetSmoothnessRock, w);
                m_RockLayer.diffuseRemapMax = Color.Lerp(m_RockRemap0, m_WetTintRock, w);
            }
            if (m_WetterMaterials != null && m_MatSmooth0 != null)
            {
                for (int i = 0; i < m_WetterMaterials.Length; i++)
                {
                    Material m = m_WetterMaterials[i];
                    if (m != null && m.HasProperty("_Smoothness"))
                    {
                        m.SetFloat("_Smoothness", Mathf.Min(1f, m_MatSmooth0[i] + m_MaterialSmoothnessBoost * w));
                    }
                }
            }
        }
    }
}
