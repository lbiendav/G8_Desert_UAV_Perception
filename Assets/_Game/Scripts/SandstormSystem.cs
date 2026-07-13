using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

namespace DesertEnv
{
    /// <summary>
    /// Sandstorm for the HDRP desert. Blends in a runtime-built global Volume
    /// whose Fog override chokes visibility down to a dusty orange soup and
    /// dims and warms the sun (through DayNightCycle.SunDimming01). A single
    /// Star_pack fog.vfx cloud follows the main camera for local billow
    /// detail - one modest emitter instead of a terrain-wide grid, because
    /// stacked fullscreen alpha layers are a GPU killer. Everything is
    /// created at Start and blended by Intensity01, so no shared asset is
    /// ever modified.
    /// </summary>
    [DisallowMultipleComponent]
    public class SandstormSystem : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Terrain m_Terrain;
        [SerializeField] private DayNightCycle m_DayNight;

        [Header("Blend")]
        [SerializeField] private float m_RampSeconds = 20f;

        [Header("Fog")]
        [SerializeField] private float m_StormVisibilityMeters = 22f;
        [Tooltip("Fog layer top. Keep it above the highest camera so the horizon is fully covered during a storm.")]
        [SerializeField] private float m_StormFogHeightMeters = 300f;
        [SerializeField] private Color m_DustColor = new Color(0.72f, 0.53f, 0.31f);

        [Header("Sun")]
        [Tooltip("How much of the sun the dust swallows. Past ~0.6 the physically based sky goes unnaturally black.")]
        [SerializeField, Range(0f, 1f)] private float m_MaxSunDimming = 0.55f;

        [Header("Dust cloud (Star_pack fog.vfx, camera-following)")]
        [SerializeField] private VisualEffectAsset m_DustVfxAsset;
        [SerializeField] private Vector3 m_DustBoxSize = new Vector3(140f, 30f, 140f);
        [SerializeField] private float m_MaxDustAmount = 35f;
        [SerializeField] private int m_RandomSeed = 913;

        public float Intensity01 { get; private set; }

        /// <summary>Set by the WeatherDirector; 0 = clear air, 1 = full storm.</summary>
        public float TargetIntensity01 { get; set; }

        public bool IsActive => Intensity01 > 0.02f;

        private Volume m_Volume;
        private Fog m_Fog;
        private VisualEffect m_DustCloud;
        private Transform m_DustTransform;

        private static readonly int s_AmountId = Shader.PropertyToID("amount");
        private static readonly int s_BoxSizeId = Shader.PropertyToID("Box size");

        private void Awake()
        {
            if (m_Terrain == null) m_Terrain = Terrain.activeTerrain;
            if (m_DayNight == null) m_DayNight = FindFirstObjectByType<DayNightCycle>();
        }

        private void Start()
        {
            BuildVolume();
            BuildDustCloud();
            Apply();
        }

        private void OnDisable()
        {
            TargetIntensity01 = 0f;
            Intensity01 = 0f;
            Apply();
        }

        private void Update()
        {
            float ramp = m_RampSeconds > 0.01f ? Time.deltaTime / m_RampSeconds : 1f;
            Intensity01 = Mathf.MoveTowards(Intensity01, Mathf.Clamp01(TargetIntensity01), ramp);
            Apply();
        }

        /// <summary>
        /// Global volume with a Fog override, blended over the scene's own
        /// Sky and Fog Volume by weight. The profile is created in code so
        /// no project asset is touched.
        /// </summary>
        private void BuildVolume()
        {
            var go = new GameObject("SandstormVolume (runtime)");
            go.transform.SetParent(transform, false);

            m_Volume = go.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 60f; // above the scene's Sky and Fog Volume
            m_Volume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SandstormProfile (runtime)";

            m_Fog = profile.Add<Fog>();
            m_Fog.enabled.Override(true);
            m_Fog.meanFreePath.Override(m_StormVisibilityMeters);
            m_Fog.baseHeight.Override(0f);
            m_Fog.maximumHeight.Override(m_StormFogHeightMeters);
            // SkyColor mode, not ConstantColor: the constant color would
            // blend from the base profile's unset (near-black) color at low
            // weights and paint a black band on the horizon
            m_Fog.colorMode.Override(FogColorMode.SkyColor);
            m_Fog.tint.Override(m_DustColor);
            m_Fog.albedo.Override(m_DustColor);
            m_Fog.enableVolumetricFog.Override(true);

            m_Volume.profile = profile;
        }

        /// <summary>
        /// One camera-following fog.vfx cloud for local billow detail.
        /// The heavy lifting (visibility, tint) is done by the fog volume.
        /// </summary>
        private void BuildDustCloud()
        {
            if (m_DustVfxAsset == null)
            {
                Debug.LogWarning("[SandstormSystem] No dust VFX asset assigned - storm will be fog only.", this);
                return;
            }

            var cloudGo = new GameObject("DustCloud (runtime)");
            cloudGo.transform.SetParent(transform, false);
            m_DustTransform = cloudGo.transform;

            m_DustCloud = cloudGo.AddComponent<VisualEffect>();
            m_DustCloud.visualEffectAsset = m_DustVfxAsset;
            m_DustCloud.resetSeedOnPlay = false;
            m_DustCloud.startSeed = (uint)m_RandomSeed;

            if (m_DustCloud.HasVector3(s_BoxSizeId))
            {
                m_DustCloud.SetVector3(s_BoxSizeId, m_DustBoxSize);
            }

            cloudGo.SetActive(false);
        }

        private void Apply()
        {
            if (m_Volume != null)
            {
                m_Volume.weight = Intensity01;
            }

            if (m_DayNight != null)
            {
                m_DayNight.SunDimming01 = Intensity01 * m_MaxSunDimming;
            }

            if (m_DustCloud == null)
            {
                return;
            }

            bool visible = Intensity01 > 0.01f;
            if (m_DustTransform.gameObject.activeSelf != visible)
            {
                m_DustTransform.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            // keep the cloud around the camera so the viewer sits inside the
            // billows, but never let it sink below the local ground
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                float y = p.y - 6f;
                if (m_Terrain != null)
                {
                    float ground = m_Terrain.SampleHeight(p) + m_Terrain.transform.position.y;
                    y = Mathf.Max(y, ground + m_DustBoxSize.y * 0.3f);
                }
                m_DustTransform.position = new Vector3(p.x, y, p.z);
            }

            float amount = m_MaxDustAmount * Intensity01;
            if (m_DustCloud.HasFloat(s_AmountId))
            {
                m_DustCloud.SetFloat(s_AmountId, amount);
            }
            else if (m_DustCloud.HasInt(s_AmountId))
            {
                m_DustCloud.SetInt(s_AmountId, Mathf.RoundToInt(amount));
            }
            else if (m_DustCloud.HasVector2(s_AmountId))
            {
                m_DustCloud.SetVector2(s_AmountId, new Vector2(amount, amount));
            }
        }
    }
}
