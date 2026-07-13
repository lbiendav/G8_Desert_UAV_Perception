using UnityEngine;

namespace DesertEnv
{
    public enum DesertWeather
    {
        Clear = 0,
        Breezy = 1,
        Windstorm = 2,
        Sandstorm = 3,
        Rain = 4
    }

    /// <summary>
    /// The single brain that decides what the desert weather is doing.
    /// Runs a small Markov chain over Clear / Breezy / Windstorm / Sandstorm
    /// / Rain, holds each state for a random duration, then rolls the next
    /// state from a per-state transition table (a windstorm likes to grow
    /// into a sandstorm, rain clears up, and so on). It never renders
    /// anything itself - it only sets targets on the specialist systems:
    /// WeatherSystem (rain), SandstormSystem (dust) and DesertWindController
    /// (wind), which each ramp smoothly toward those targets.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class WeatherDirector : MonoBehaviour
    {
        public static WeatherDirector Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private WeatherSystem m_RainSystem;
        [SerializeField] private SandstormSystem m_Sandstorm;
        [SerializeField] private DesertWindController m_Wind;

        [Header("Cycle")]
        [SerializeField] private bool m_AutoCycle = true;
        [SerializeField] private DesertWeather m_StartWeather = DesertWeather.Clear;

        public DesertWeather Current { get; private set; }
        public float SecondsUntilChange => m_Timer;

        /// <summary>Turn the automatic Markov cycle on/off at runtime
        /// (used by the WeatherControlPanel). Off = the current weather
        /// holds until SetWeather is called.</summary>
        public bool AutoCycle
        {
            get => m_AutoCycle;
            set => m_AutoCycle = value;
        }

        private float m_Timer;

        private struct StateSpec
        {
            public Vector2 duration;   // min/max seconds the state holds
            public float wind;         // DesertWindController target
            public float storm;        // SandstormSystem target
            public bool rain;

            public StateSpec(float minDur, float maxDur, float wind, float storm, bool rain)
            {
                duration = new Vector2(minDur, maxDur);
                this.wind = wind;
                this.storm = storm;
                this.rain = rain;
            }
        }

        // indexed by (int)DesertWeather
        private static readonly StateSpec[] s_Specs =
        {
            new StateSpec(90f, 200f, 0.12f, 0f,    false), // Clear
            new StateSpec(70f, 150f, 0.38f, 0f,    false), // Breezy
            new StateSpec(45f, 100f, 0.78f, 0.22f, false), // Windstorm (dusty haze builds)
            new StateSpec(50f, 110f, 1.00f, 1f,    false), // Sandstorm
            new StateSpec(40f,  85f, 0.45f, 0f,    true),  // Rain
        };

        // Markov transition weights, row = current state, column = next state
        private static readonly float[][] s_Transitions =
        {
            //             Clear Breezy Windst Sandst Rain
            new[] { 0.00f, 0.55f, 0.15f, 0.10f, 0.20f }, // from Clear
            new[] { 0.40f, 0.00f, 0.30f, 0.15f, 0.15f }, // from Breezy
            new[] { 0.20f, 0.35f, 0.00f, 0.45f, 0.00f }, // from Windstorm
            new[] { 0.20f, 0.45f, 0.35f, 0.00f, 0.00f }, // from Sandstorm
            new[] { 0.55f, 0.45f, 0.00f, 0.00f, 0.00f }, // from Rain
        };

        private void Awake()
        {
            Instance = this;
            if (m_RainSystem == null) m_RainSystem = FindFirstObjectByType<WeatherSystem>();
            if (m_Sandstorm == null) m_Sandstorm = FindFirstObjectByType<SandstormSystem>();
            if (m_Wind == null) m_Wind = FindFirstObjectByType<DesertWindController>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            // the director owns the rain cycle from now on
            if (m_RainSystem != null)
            {
                m_RainSystem.AutoCycle = false;
            }
            SetWeather(m_StartWeather);
        }

        private void Update()
        {
            if (!m_AutoCycle)
            {
                return;
            }

            m_Timer -= Time.deltaTime;
            if (m_Timer <= 0f)
            {
                SetWeather(RollNext(Current));
            }
        }

        /// <summary>Forces a weather state (also used by the auto cycle).</summary>
        public void SetWeather(DesertWeather weather)
        {
            Current = weather;
            StateSpec spec = s_Specs[(int)weather];
            m_Timer = Random.Range(spec.duration.x, spec.duration.y);

            if (m_Wind != null)
            {
                m_Wind.TargetStrength01 = spec.wind;
            }
            if (m_Sandstorm != null)
            {
                m_Sandstorm.TargetIntensity01 = spec.storm;
            }
            if (m_RainSystem != null && m_RainSystem.IsRaining != spec.rain)
            {
                m_RainSystem.SetRaining(spec.rain);
            }

            Debug.Log($"[WeatherDirector] Weather -> {weather} for {m_Timer:0}s");
        }

        private static DesertWeather RollNext(DesertWeather from)
        {
            float[] row = s_Transitions[(int)from];
            float total = 0f;
            for (int i = 0; i < row.Length; i++)
            {
                total += row[i];
            }

            float roll = Random.value * total;
            for (int i = 0; i < row.Length; i++)
            {
                roll -= row[i];
                if (roll <= 0f && row[i] > 0f)
                {
                    return (DesertWeather)i;
                }
            }
            return DesertWeather.Clear;
        }

        [ContextMenu("Force Clear")]
        private void ForceClear() => SetWeather(DesertWeather.Clear);

        [ContextMenu("Force Breezy")]
        private void ForceBreezy() => SetWeather(DesertWeather.Breezy);

        [ContextMenu("Force Windstorm")]
        private void ForceWindstorm() => SetWeather(DesertWeather.Windstorm);

        [ContextMenu("Force Sandstorm")]
        private void ForceSandstorm() => SetWeather(DesertWeather.Sandstorm);

        [ContextMenu("Force Rain")]
        private void ForceRain() => SetWeather(DesertWeather.Rain);
    }
}
