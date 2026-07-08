using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Procedural wind sway for static tree meshes (HDRP/Lit has no built-in
    /// vertex wind). Tilts the whole tree around its base along the WindZone
    /// direction using a sine + Perlin gust mix. Reads windMain/windTurbulence
    /// from the scene WindZone so all trees respond to the same weather.
    /// </summary>
    [DisallowMultipleComponent]
    public class TreeWindSway : MonoBehaviour
    {
        [SerializeField] private float m_MaxSwayDegrees = 3.0f;
        [SerializeField] private float m_SwayFrequency = 0.55f;
        [SerializeField, Range(0f, 1f)] private float m_GustWeight = 0.55f;
        [SerializeField] private float m_SideWobbleRatio = 0.35f;

        private static WindZone s_WindZone;
        private static bool s_Searched;

        private Quaternion m_BaseRotation;
        private float m_Seed;

        private void Awake()
        {
            m_BaseRotation = transform.localRotation;
            m_Seed = Random.Range(0f, 512f);

            if (!s_Searched || s_WindZone == null)
            {
                s_WindZone = FindFirstObjectByType<WindZone>();
                s_Searched = true;
            }
        }

        private void Update()
        {
            float main = 1f;
            float turbulence = 0.5f;
            Vector3 windDir = new Vector3(0.4f, 0f, 0.9f);

            if (s_WindZone != null)
            {
                main = Mathf.Max(0f, s_WindZone.windMain);
                turbulence = Mathf.Max(0f, s_WindZone.windTurbulence);
                windDir = s_WindZone.transform.forward;
            }

            windDir.y = 0f;
            if (windDir.sqrMagnitude < 1e-4f)
            {
                windDir = Vector3.forward;
            }
            windDir.Normalize();

            float t = Time.time * m_SwayFrequency + m_Seed;
            float wave = Mathf.Sin(t * Mathf.PI * 2f * 0.35f);
            float gust = (Mathf.PerlinNoise(t, m_Seed) * 2f - 1f) * (0.5f + turbulence);
            float osc = Mathf.Lerp(wave, gust, m_GustWeight);

            // lean slightly downwind on average, oscillate around that lean
            float along = m_MaxSwayDegrees * main * (0.35f + 0.65f * osc);
            float side = m_MaxSwayDegrees * main * m_SideWobbleRatio *
                         Mathf.Sin(t * Mathf.PI * 2f * 0.23f + m_Seed * 0.7f);

            Vector3 leanAxis = Vector3.Cross(Vector3.up, windDir);
            Quaternion sway = Quaternion.AngleAxis(along, leanAxis) *
                              Quaternion.AngleAxis(side, windDir);
            transform.localRotation = sway * m_BaseRotation;
        }
    }
}
