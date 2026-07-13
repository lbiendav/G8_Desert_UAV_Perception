using UnityEngine;
using UnityEngine.VFX;

namespace DesertEnv
{
    /// <summary>
    /// Rain weather built from the Star_pack assets. While it rains:
    ///  - a static grid of Star_pack "Rain.vfx" cells covers the WHOLE
    ///    terrain (world-space rain, not attached to any camera). Each cell
    ///    hugs the local ground height, its "Amount" scales with intensity,
    ///    and every camera frustum-culls the cells it cannot see, so the
    ///    cost stays low;
    ///  - the terrain layers get darker and glossier for a wet water-sheen
    ///    look, and
    ///  - random puddles (planes with the Star_pack "puddle" shader) grow at
    ///    flat spots of the terrain, each with its own soak threshold so they
    ///    pool one after another.
    /// When the rain stops the grid turns off and everything dries out
    /// slowly, faster under a high sun. Shared assets (terrain layers,
    /// materials) are cached on enable and restored on disable so they are
    /// never permanently altered.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Cycle")]
        [SerializeField] private bool m_AutoCycle = true;
        [SerializeField] private Vector2 m_DrySecondsRange = new Vector2(70f, 160f);
        [SerializeField] private Vector2 m_RainSecondsRange = new Vector2(40f, 80f);
        [SerializeField] private float m_RainRampSeconds = 6f;

        [Header("Rain VFX (Star_pack Rain.vfx)")]
        [Tooltip("Inactive template instance; the grid clones it across the terrain at Start.")]
        [SerializeField] private VisualEffect m_RainVfx;
        [Tooltip("Rain 'Amount' PER GRID CELL at full intensity.")]
        [SerializeField] private Vector2 m_MaxRainAmount = new Vector2(400f, 400f);
        [Tooltip("The terrain is split into cells x cells rain volumes.")]
        [SerializeField, Range(1, 8)] private int m_RainGridCells = 5;
        [SerializeField] private float m_RainBoxHeight = 70f;
        [SerializeField] private Terrain m_Terrain;

        [Header("Wet ground sheen")]
        [SerializeField] private DayNightCycle m_DayNight;
        [SerializeField] private float m_SoakSeconds = 12f;
        [SerializeField] private float m_DryOutSeconds = 45f;
        [SerializeField] private TerrainLayer m_SandLayer;
        [SerializeField] private TerrainLayer m_RockLayer;
        [SerializeField] private float m_WetSmoothnessSand = 0.55f;
        [SerializeField] private float m_WetSmoothnessRock = 0.45f;
        [SerializeField] private Color m_WetTintSand = new Color(0.52f, 0.50f, 0.48f, 1f);
        [SerializeField] private Color m_WetTintRock = new Color(0.58f, 0.58f, 0.60f, 1f);

        [Header("Puddles (Star_pack puddle shader)")]
        [SerializeField] private Material m_PuddleMaterial;
        [SerializeField] private int m_PuddleCount = 14;
        [SerializeField] private Vector2 m_PuddleSizeRange = new Vector2(3f, 8f);
        [SerializeField] private float m_PuddleMaxSlope = 3.5f;
        [SerializeField] private float m_PuddleMinWorldY = -1000f;
        [SerializeField] private float m_PuddleMaxWorldY = 1000f;
        [SerializeField] private float m_PuddleMinSpacing = 10f;
        [SerializeField] private float m_PuddleSpawnRadius = 120f;
        [SerializeField] private Transform m_PuddleAreaCenter;
        [SerializeField] private int m_RandomSeed = 1234;

        public bool IsRaining { get; private set; }
        public float RainIntensity01 { get; private set; }
        public float Wetness01 { get; private set; }

        /// <summary>When a WeatherDirector runs the show it turns the
        /// internal dry/rain timer off and calls SetRaining itself.</summary>
        public bool AutoCycle
        {
            get => m_AutoCycle;
            set => m_AutoCycle = value;
        }

        private static readonly int s_OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int s_AmountId = Shader.PropertyToID("Amount");
        private static readonly int s_BoxSizeId = Shader.PropertyToID("Box size");

        private struct Puddle
        {
            public Transform tf;
            public MeshRenderer rend;
            public float size;      // full diameter in meters
            public float stretch;   // anisotropic scale so shapes differ
            public float threshold; // wetness at which this puddle starts pooling
        }

        private Puddle[] m_Puddles;
        private MaterialPropertyBlock m_Mpb;
        private Transform m_PuddleRoot;

        private Transform m_RainGridRoot;
        private VisualEffect[] m_RainCells;

        private float m_PhaseTimer;
        private float m_SandSmooth0;
        private float m_RockSmooth0;
        private Color m_SandRemap0;
        private Color m_RockRemap0;

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
        }

        private void Start()
        {
            SpawnPuddles();
            BuildRainGrid();
            ApplyPuddles();
            ApplyRain();
        }

        /// <summary>Force rain on/off (also usable from other scripts or UI).</summary>
        public void SetRaining(bool raining)
        {
            IsRaining = raining;
            m_PhaseTimer = raining
                ? Random.Range(m_RainSecondsRange.x, m_RainSecondsRange.y)
                : Random.Range(m_DrySecondsRange.x, m_DrySecondsRange.y);
        }

        [ContextMenu("Start Rain")]
        private void StartRainNow() => SetRaining(true);

        [ContextMenu("Stop Rain")]
        private void StopRainNow() => SetRaining(false);

        private void Update()
        {
            float dt = Time.deltaTime;

            if (m_AutoCycle)
            {
                m_PhaseTimer -= dt;
                if (m_PhaseTimer <= 0f)
                {
                    SetRaining(!IsRaining);
                }
            }

            // ramp the rain intensity in/out
            float targetIntensity = IsRaining ? 1f : 0f;
            float ramp = m_RainRampSeconds > 0.01f ? dt / m_RainRampSeconds : 1f;
            RainIntensity01 = Mathf.MoveTowards(RainIntensity01, targetIntensity, ramp);

            // soak while raining, dry out otherwise (faster under a high sun)
            if (RainIntensity01 > 0.01f)
            {
                Wetness01 += dt / Mathf.Max(1f, m_SoakSeconds) * RainIntensity01;
            }
            else
            {
                float sun = m_DayNight != null ? m_DayNight.DaylightFactor : 1f;
                Wetness01 -= dt / Mathf.Max(1f, m_DryOutSeconds) * (0.35f + sun * 1.3f);
            }
            Wetness01 = Mathf.Clamp01(Wetness01);

            ApplyWetLook();
            ApplyPuddles();
            ApplyRain();
        }

        /// <summary>
        /// Clones the Rain.vfx template into a static world-space grid that
        /// covers the whole terrain. Each cell sits over the average local
        /// ground height so the rain band always reaches the surface.
        /// </summary>
        private void BuildRainGrid()
        {
            if (m_RainVfx == null || m_Terrain == null)
            {
                return;
            }

            TerrainData data = m_Terrain.terrainData;
            Vector3 tPos = m_Terrain.transform.position;
            Vector3 tSize = data.size;

            m_RainGridRoot = new GameObject("RainGrid").transform;
            m_RainGridRoot.SetParent(transform, false);

            int n = Mathf.Max(1, m_RainGridCells);
            float cellX = tSize.x / n;
            float cellZ = tSize.z / n;

            m_RainCells = new VisualEffect[n * n];
            int idx = 0;

            for (int zi = 0; zi < n; zi++)
            {
                for (int xi = 0; xi < n; xi++)
                {
                    float cx = tPos.x + (xi + 0.5f) * cellX;
                    float cz = tPos.z + (zi + 0.5f) * cellZ;

                    // average ground height from a 3x3 sample of the cell
                    float ground = 0f;
                    for (int sz = 0; sz < 3; sz++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float wx = tPos.x + (xi + 0.25f * (sx + 1)) * cellX;
                            float wz = tPos.z + (zi + 0.25f * (sz + 1)) * cellZ;
                            ground += m_Terrain.SampleHeight(new Vector3(wx, 0f, wz));
                        }
                    }
                    ground = ground / 9f + tPos.y;

                    GameObject cellGo = Instantiate(m_RainVfx.gameObject, m_RainGridRoot);
                    cellGo.name = "RainCell_" + xi + "_" + zi;
                    cellGo.transform.position = new Vector3(cx, ground + m_RainBoxHeight * 0.5f + 8f, cz);

                    VisualEffect cell = cellGo.GetComponent<VisualEffect>();
                    cell.resetSeedOnPlay = false;
                    cell.startSeed = (uint)(xi * 73856093 ^ zi * 19349663 ^ m_RandomSeed);
                    if (cell.HasVector3(s_BoxSizeId))
                    {
                        cell.SetVector3(s_BoxSizeId, new Vector3(cellX, m_RainBoxHeight, cellZ));
                    }

                    cellGo.SetActive(true);
                    m_RainCells[idx++] = cell;
                }
            }

            // the template itself never plays; the grid root is toggled by rain
            m_RainVfx.gameObject.SetActive(false);
            m_RainGridRoot.gameObject.SetActive(false);
        }

        private void ApplyRain()
        {
            if (m_RainGridRoot == null)
            {
                return;
            }

            bool visible = RainIntensity01 > 0.005f;
            if (m_RainGridRoot.gameObject.activeSelf != visible)
            {
                m_RainGridRoot.gameObject.SetActive(visible);
            }

            if (!visible || m_RainCells == null)
            {
                return;
            }

            Vector2 amount = m_MaxRainAmount * RainIntensity01;
            for (int i = 0; i < m_RainCells.Length; i++)
            {
                VisualEffect cell = m_RainCells[i];
                if (cell != null && cell.HasVector2(s_AmountId))
                {
                    cell.SetVector2(s_AmountId, amount);
                }
            }
        }

        private void ApplyWetLook()
        {
            float w = Wetness01;

            if (m_SandLayer != null)
            {
                m_SandLayer.smoothness = Mathf.Lerp(m_SandSmooth0, m_WetSmoothnessSand, w);
                m_SandLayer.diffuseRemapMax = Color.Lerp(m_SandRemap0, m_WetTintSand, w * 0.85f);
            }
            if (m_RockLayer != null)
            {
                m_RockLayer.smoothness = Mathf.Lerp(m_RockSmooth0, m_WetSmoothnessRock, w);
                m_RockLayer.diffuseRemapMax = Color.Lerp(m_RockRemap0, m_WetTintRock, w * 0.85f);
            }
        }

        private void SpawnPuddles()
        {
            if (m_Terrain == null || m_PuddleMaterial == null || m_PuddleCount <= 0)
            {
                return;
            }

            m_Mpb = new MaterialPropertyBlock();
            m_PuddleRoot = new GameObject("Puddles").transform;
            m_PuddleRoot.SetParent(transform, false);

            TerrainData data = m_Terrain.terrainData;
            Vector3 tPos = m_Terrain.transform.position;
            Vector3 tSize = data.size;
            Vector3 areaCenter = m_PuddleAreaCenter != null
                ? m_PuddleAreaCenter.position
                : tPos + new Vector3(tSize.x * 0.5f, 0f, tSize.z * 0.5f);

            Random.State restore = Random.state;
            Random.InitState(m_RandomSeed);

            var puddles = new System.Collections.Generic.List<Puddle>(m_PuddleCount);
            int attempts = m_PuddleCount * 40;

            while (puddles.Count < m_PuddleCount && attempts-- > 0)
            {
                Vector2 disc = Random.insideUnitCircle * m_PuddleSpawnRadius;
                float worldX = areaCenter.x + disc.x;
                float worldZ = areaCenter.z + disc.y;

                float nx = Mathf.InverseLerp(tPos.x, tPos.x + tSize.x, worldX);
                float nz = Mathf.InverseLerp(tPos.z, tPos.z + tSize.z, worldZ);
                if (nx <= 0.02f || nx >= 0.98f || nz <= 0.02f || nz >= 0.98f)
                {
                    continue;
                }

                if (data.GetSteepness(nx, nz) > m_PuddleMaxSlope)
                {
                    continue;
                }

                float worldY = m_Terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + tPos.y;
                if (worldY < m_PuddleMinWorldY || worldY > m_PuddleMaxWorldY)
                {
                    continue;
                }

                bool tooClose = false;
                for (int i = 0; i < puddles.Count; i++)
                {
                    Vector3 other = puddles[i].tf.position;
                    float dx = other.x - worldX;
                    float dz = other.z - worldZ;
                    if (dx * dx + dz * dz < m_PuddleMinSpacing * m_PuddleMinSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                {
                    continue;
                }

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                go.name = "Puddle_" + puddles.Count;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(m_PuddleRoot, false);

                Vector3 normal = data.GetInterpolatedNormal(nx, nz);
                go.transform.SetPositionAndRotation(
                    new Vector3(worldX, worldY + 0.05f, worldZ),
                    Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                var rend = go.GetComponent<MeshRenderer>();
                rend.sharedMaterial = m_PuddleMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                puddles.Add(new Puddle
                {
                    tf = go.transform,
                    rend = rend,
                    size = Random.Range(m_PuddleSizeRange.x, m_PuddleSizeRange.y),
                    stretch = Random.Range(0.78f, 1.28f),
                    threshold = Random.Range(0.15f, 0.6f)
                });
            }

            Random.state = restore;
            m_Puddles = puddles.ToArray();
        }

        private void ApplyPuddles()
        {
            if (m_Puddles == null)
            {
                return;
            }

            for (int i = 0; i < m_Puddles.Length; i++)
            {
                Puddle p = m_Puddles[i];
                if (p.tf == null)
                {
                    continue;
                }

                // each puddle pools once the ground is soaked past its own
                // threshold, growing to full size over a 0.35 wetness window
                float k = Mathf.InverseLerp(p.threshold, Mathf.Min(p.threshold + 0.35f, 1f), Wetness01);
                bool visible = k > 0.01f;

                if (p.tf.gameObject.activeSelf != visible)
                {
                    p.tf.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                float grow = Mathf.SmoothStep(0f, 1f, k);
                // the built-in plane is 10x10 units at scale 1
                float scale = p.size * 0.1f * Mathf.Lerp(0.35f, 1f, grow);
                p.tf.localScale = new Vector3(scale * p.stretch, 1f, scale / p.stretch);

                p.rend.GetPropertyBlock(m_Mpb);
                m_Mpb.SetFloat(s_OpacityId, k);
                p.rend.SetPropertyBlock(m_Mpb);
            }
        }
    }
}
