using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DesertEnv
{
    /// <summary>
    /// Overcast sky that rolls in with the rain. A runtime-built global
    /// Volume switches the VisualEnvironment cloud type to CloudLayer (the
    /// base Sky and Fog Volume has none) and fades a grey two-channel
    /// cloud deck in through the layer's opacity, while the sun dims
    /// through DayNightCycle.OvercastDimming01. The volume weight is a
    /// hard on/off switch because cloudType cannot blend - the smooth
    /// fade lives entirely in the opacity parameter, which this component
    /// drives directly on its own runtime profile.
    /// </summary>
    [DisallowMultipleComponent]
    public class RainCloudSystem : MonoBehaviour
    {
        [SerializeField] private WeatherSystem m_Weather;
        [SerializeField] private DayNightCycle m_DayNight;

        [Header("Blend")]
        [SerializeField] private float m_RampSeconds = 15f;

        [Header("Cloud deck")]
        [SerializeField] private Color m_CloudTint = new Color(0.38f, 0.38f, 0.42f);
        [SerializeField, Range(0f, 1f)] private float m_MaxSunDimming = 0.7f;

        public float Intensity01 { get; private set; }

        private Volume m_Volume;
        private CloudLayer m_Clouds;

        private void Awake()
        {
            if (m_Weather == null) m_Weather = FindFirstObjectByType<WeatherSystem>();
            if (m_DayNight == null) m_DayNight = FindFirstObjectByType<DayNightCycle>();
        }

        private void Start()
        {
            BuildVolume();
            Apply();
        }

        private void OnDisable()
        {
            Intensity01 = 0f;
            Apply();
        }

        private void Update()
        {
            float target = 0f;
            if (m_Weather != null)
            {
                // clouds gather as soon as the rain state starts, and hang
                // around while the last drops are still falling
                target = Mathf.Max(m_Weather.IsRaining ? 1f : 0f, m_Weather.RainIntensity01);
            }

            float ramp = m_RampSeconds > 0.01f ? Time.deltaTime / m_RampSeconds : 1f;
            Intensity01 = Mathf.MoveTowards(Intensity01, Mathf.Clamp01(target), ramp);
            Apply();
        }

        private void BuildVolume()
        {
            var go = new GameObject("RainCloudVolume (runtime)");
            go.transform.SetParent(transform, false);

            m_Volume = go.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 55f; // above the base sky, below the sandstorm
            m_Volume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RainCloudProfile (runtime)";

            // the base profile renders no clouds, so this volume must flip
            // the cloud type on while it is active
            var env = profile.Add<VisualEnvironment>();
            env.cloudType.Override((int)CloudType.CloudLayer);

            m_Clouds = profile.Add<CloudLayer>();
            m_Clouds.opacity.Override(0f);
            m_Clouds.upperHemisphereOnly.Override(true);

            CloudLayer.CloudMap deck = m_Clouds.layerA;
            deck.opacityR.Override(1f);   // cumulus channel of the default map
            deck.opacityG.Override(1f);   // stratus channel - together: overcast
            deck.opacityB.Override(0f);
            deck.opacityA.Override(0f);
            deck.tint.Override(m_CloudTint);
            deck.lighting.Override(true);

            m_Volume.profile = profile;
        }

        private void Apply()
        {
            if (m_Volume != null)
            {
                // hard switch: cloudType is not blendable, opacity does the fade
                m_Volume.weight = Intensity01 > 0.002f ? 1f : 0f;
                m_Clouds.opacity.value = Intensity01;
            }

            if (m_DayNight != null)
            {
                m_DayNight.OvercastDimming01 = Intensity01 * m_MaxSunDimming;
            }
        }
    }
}
