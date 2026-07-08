using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Desert heat shimmer. Drives a ground-hugging particle system whose
    /// material writes into HDRP's distortion buffer (screen-space refraction
    /// wobble). Strength follows the sun: full at high noon, zero at night,
    /// and suppressed while it rains or while the ground is still wet.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeatHaze : MonoBehaviour
    {
        [SerializeField] private ParticleSystem m_HazeParticles;
        [SerializeField] private DayNightCycle m_DayNight;
        [SerializeField] private WeatherSystem m_Weather;
        [SerializeField] private Transform m_FollowTarget;
        [SerializeField] private Terrain m_Terrain;
        [SerializeField] private float m_MaxEmission = 60f;
        [SerializeField] private float m_GroundOffset = 3f;

        public float Strength01 { get; private set; }

        private void Update()
        {
            float day = m_DayNight != null ? Mathf.Pow(m_DayNight.DaylightFactor, 1.4f) : 1f;
            float rain = m_Weather != null ? m_Weather.RainIntensity01 : 0f;
            float wet = m_Weather != null ? m_Weather.Wetness01 : 0f;

            Strength01 = day * (1f - rain) * (1f - 0.85f * wet);
            if (Strength01 < 0.03f)
            {
                Strength01 = 0f;
            }

            if (m_HazeParticles != null)
            {
                ParticleSystem.EmissionModule em = m_HazeParticles.emission;
                em.rateOverTime = m_MaxEmission * Strength01;
            }
        }

        private void LateUpdate()
        {
            if (m_FollowTarget == null || m_HazeParticles == null)
            {
                return;
            }

            // keep the shimmer volume hovering over the ground under the camera
            Vector3 p = m_FollowTarget.position;
            float ground = m_Terrain != null
                ? m_Terrain.SampleHeight(p) + m_Terrain.transform.position.y
                : 0f;
            m_HazeParticles.transform.position = new Vector3(p.x, ground + m_GroundOffset, p.z);
        }
    }
}
