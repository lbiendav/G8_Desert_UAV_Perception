using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Rotates the sun through a full day cycle and drives its intensity and
    /// color temperature. Works with HDRP's Physically Based Sky, so dawn,
    /// dusk and night lighting come from the sky model itself. A low-lux moon
    /// light takes over at night. Exposes DaylightFactor (0 = night,
    /// 1 = high noon) for other systems (heat haze, weather drying).
    /// </summary>
    [DisallowMultipleComponent]
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Cycle")]
        [SerializeField] private float m_DayLengthSeconds = 480f;
        [SerializeField, Range(0f, 1f)] private float m_TimeOfDay = 0.35f; // 0 = midnight, 0.5 = noon

        [Header("Lights")]
        [SerializeField] private Light m_Sun;
        [SerializeField] private Light m_Moon;
        [SerializeField] private float m_SunYawDegrees = 200f;
        [SerializeField] private float m_SunPeakLux = 130000f;
        [SerializeField] private float m_MoonLux = 1.5f;

        public float TimeOfDay01 => m_TimeOfDay;
        public float DaylightFactor { get; private set; }
        public bool IsNight => DaylightFactor < 0.02f;

        /// <summary>0 = clear sky, 1 = sun fully choked by dust.
        /// Set by the SandstormSystem while a storm blows.</summary>
        public float SunDimming01 { get; set; }

        /// <summary>0 = clear sky, 1 = thick rain clouds blocking the sun.
        /// Set by the RainCloudSystem while it rains.</summary>
        public float OvercastDimming01 { get; set; }

        public void SetTimeOfDay(float t01)
        {
            m_TimeOfDay = Mathf.Repeat(t01, 1f);
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (m_DayLengthSeconds > 1f)
            {
                m_TimeOfDay = Mathf.Repeat(m_TimeOfDay + Time.deltaTime / m_DayLengthSeconds, 1f);
            }
            Apply();
        }

        private void Apply()
        {
            float sunPitch = m_TimeOfDay * 360f - 90f;

            if (m_Sun != null)
            {
                m_Sun.transform.rotation = Quaternion.Euler(sunPitch, m_SunYawDegrees, 0f);
                DaylightFactor = Mathf.Clamp01(-m_Sun.transform.forward.y * 1.25f);

                float dim = Mathf.Clamp01(SunDimming01);
                float overcast = Mathf.Clamp01(OvercastDimming01);
                m_Sun.intensity = m_SunPeakLux * Mathf.Pow(Mathf.Max(DaylightFactor, 0.0001f), 1.15f)
                                  * (1f - dim * 0.92f) * (1f - overcast * 0.85f);
                float kelvin = Mathf.Lerp(1900f, 6500f, Mathf.Clamp01(DaylightFactor * 1.8f));
                // suspended dust filters the light toward a deep amber
                m_Sun.colorTemperature = Mathf.Lerp(kelvin, 2600f, dim * 0.8f);
                // below the horizon the sun must not light the scene from underneath
                m_Sun.enabled = DaylightFactor > 0.002f;
            }

            if (m_Moon != null)
            {
                m_Moon.transform.rotation = Quaternion.Euler(sunPitch + 180f, m_SunYawDegrees - 40f, 0f);
                bool moonUp = DaylightFactor < 0.05f;
                m_Moon.enabled = moonUp;
                if (moonUp)
                {
                    m_Moon.intensity = m_MoonLux;
                }
            }
        }
    }
}
