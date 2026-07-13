using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Drives the strength of the HDRP heat haze material. Full shimmer at
    /// high noon, none at night, suppressed while it rains or while the
    /// ground is still wet. The material asset is shared, so its pristine
    /// value is cached on enable and restored on disable.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeatHazeDriver : MonoBehaviour
    {
        [SerializeField] private Material m_HazeMaterial;
        [SerializeField] private DayNightCycle m_DayNight;
        [SerializeField] private WeatherSystem m_Weather;
        [Tooltip("Optional; when present the haze follows real air temperature instead of raw sun angle.")]
        [SerializeField] private AirTemperatureModel m_Temperature;
        [SerializeField, Range(0f, 1f)] private float m_MaxStrength = 1f;

        public float Strength01 { get; private set; }

        private static readonly int s_GlobalStrengthId = Shader.PropertyToID("_GlobalStrength");
        private float m_InitialStrength = 1f;

        private void OnEnable()
        {
            if (m_HazeMaterial != null && m_HazeMaterial.HasProperty(s_GlobalStrengthId))
            {
                m_InitialStrength = m_HazeMaterial.GetFloat(s_GlobalStrengthId);
            }
            Apply();
        }

        private void OnDisable()
        {
            if (m_HazeMaterial != null && m_HazeMaterial.HasProperty(s_GlobalStrengthId))
            {
                m_HazeMaterial.SetFloat(s_GlobalStrengthId, m_InitialStrength);
            }
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (m_Temperature == null)
            {
                m_Temperature = AirTemperatureModel.Instance;
            }

            // hot air makes the shimmer; fall back to sun angle when no
            // temperature model is in the scene
            float heat = m_Temperature != null
                ? m_Temperature.Heat01
                : (m_DayNight != null ? Mathf.Pow(m_DayNight.DaylightFactor, 1.4f) : 1f);
            float rain = m_Weather != null ? m_Weather.RainIntensity01 : 0f;
            float wet = m_Weather != null ? m_Weather.Wetness01 : 0f;

            Strength01 = Mathf.Clamp01(heat * (1f - rain) * (1f - 0.85f * wet)) * m_MaxStrength;
            if (Strength01 < 0.03f)
            {
                Strength01 = 0f;
            }

            if (m_HazeMaterial != null && m_HazeMaterial.HasProperty(s_GlobalStrengthId))
            {
                m_HazeMaterial.SetFloat(s_GlobalStrengthId, Strength01);
            }
        }
    }
}
