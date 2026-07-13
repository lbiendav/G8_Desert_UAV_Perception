using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Simple desert air temperature model. A diurnal cosine curve (coldest
    /// around 4-5 am, hottest ~1.5 h after solar noon thanks to thermal lag)
    /// is cooled by rain, soaked ground and the dust blanket of a sandstorm.
    /// Exposes Celsius plus a normalized Heat01 that the heat haze and other
    /// systems consume instead of raw sun angle.
    /// </summary>
    [DisallowMultipleComponent]
    public class AirTemperatureModel : MonoBehaviour
    {
        public static AirTemperatureModel Instance { get; private set; }

        [SerializeField] private DayNightCycle m_DayNight;
        [SerializeField] private WeatherSystem m_Weather;
        [SerializeField] private SandstormSystem m_Sandstorm;

        [Header("Diurnal curve")]
        [SerializeField] private float m_NightLowCelsius = 6f;
        [SerializeField] private float m_NoonHighCelsius = 46f;
        [SerializeField] private float m_ThermalLagHours = 1.5f;

        [Header("Weather cooling (degrees C at full effect)")]
        [SerializeField] private float m_RainCooling = 11f;
        [SerializeField] private float m_WetGroundCooling = 4f;
        [SerializeField] private float m_SandstormCooling = 7f;

        public float Celsius { get; private set; }

        /// <summary>0 at 20 C or below, 1 at 42 C or above - drives heat haze.</summary>
        public float Heat01 => Mathf.InverseLerp(20f, 42f, Celsius);

        private void Awake()
        {
            Instance = this;
            if (m_DayNight == null) m_DayNight = FindFirstObjectByType<DayNightCycle>();
            if (m_Weather == null) m_Weather = FindFirstObjectByType<WeatherSystem>();
            if (m_Sandstorm == null) m_Sandstorm = FindFirstObjectByType<SandstormSystem>();
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
            float t = m_DayNight != null ? m_DayNight.TimeOfDay01 : 0.5f;
            float lagged = Mathf.Repeat(t - m_ThermalLagHours / 24f, 1f);

            // 0 at (lagged) midnight, 1 at (lagged) noon
            float diurnal = 0.5f - 0.5f * Mathf.Cos(lagged * Mathf.PI * 2f);
            float c = Mathf.Lerp(m_NightLowCelsius, m_NoonHighCelsius, Mathf.Pow(diurnal, 1.15f));

            if (m_Weather != null)
            {
                c -= m_Weather.RainIntensity01 * m_RainCooling;
                c -= m_Weather.Wetness01 * m_WetGroundCooling;
            }
            if (m_Sandstorm != null)
            {
                c -= m_Sandstorm.Intensity01 * m_SandstormCooling;
            }

            Celsius = c;
        }
    }
}
