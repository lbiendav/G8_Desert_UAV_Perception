using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Enables its target objects only during the day or only at night,
    /// following the DayNightCycle. Drives the night sky particles (stars,
    /// shooting stars) and the daytime oasis life (butterflies, bees,
    /// pollen) so they never run when they should not be visible.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeOfDayActivator : MonoBehaviour
    {
        public enum Phase { Day, Night }

        [SerializeField] private Phase m_ActiveDuring = Phase.Day;
        [SerializeField, Range(0f, 0.5f)] private float m_DaylightThreshold = 0.08f;
        [SerializeField] private GameObject[] m_Targets;

        private DayNightCycle m_Cycle;

        private void Start()
        {
            m_Cycle = FindFirstObjectByType<DayNightCycle>();
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        private void Apply(bool force)
        {
            if (m_Cycle == null || m_Targets == null)
            {
                return;
            }

            bool day = m_Cycle.DaylightFactor > m_DaylightThreshold;
            bool wanted = m_ActiveDuring == Phase.Day ? day : !day;

            for (int i = 0; i < m_Targets.Length; i++)
            {
                GameObject go = m_Targets[i];
                if (go != null && (force || go.activeSelf != wanted))
                {
                    go.SetActive(wanted);
                }
            }
        }
    }
}
